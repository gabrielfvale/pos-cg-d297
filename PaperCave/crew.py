"""
crew.py
Paper Cave v4.1 — main entry point.

Usage:
  python crew.py                               # interactive paper folder selector
  python crew.py --paper papers/my_paper/     # specify paper folder directly
  python crew.py --simple                      # Simple Mode (Chart/Image/Table/Text)
  python crew.py --from-step extractor        # resume from Extractor
  python crew.py --from-step mapper           # resume from Mapper
  python crew.py --from-step reviewer         # resume from Reviewer

Pipeline v4.1:
  PaperFolder → Reader → Image Selector → Vision Analyst → Summarizer
              → Extractor → Mapper (+ RAG + context) ↔ Reviewer → ReviewedCardManifest

v4.1 changes vs v4:
  - Mapper produces a Card Manifest (self-contained information cards) instead
    of an Object Manifest of scene objects.
  - card_count (config) controls how many cards are generated.
  - Each card has collapsed/expanded states and contentType figure|chart|animation.
  - ReviewedCardManifest is the final output (cards + scores + implementationNotes).

LLM routing:
  Reader, Summarizer, Mapper, Vision Analyst, Reviewer → make_llm()
  Extractor, Image Selector                            → make_json_llm()
"""
import argparse
import json
import logging
import shutil
import sys
import yaml
from pathlib import Path
from datetime import datetime

from dotenv import load_dotenv
from crewai import Crew, Task, Process

from utils.config_loader import load_config, make_llm, make_json_llm, get_config_summary
from utils.pdf_selector import select_paper_folder, select_pdf
from utils.context_checker import check_and_warn
from utils.thinking_stripper import strip_thinking_tags, has_thinking_tags, extract_json_from_output
from utils.paper_context_loader import (
    load_paper_context,
    update_figure_captions,
    _build_map_task_description,
)
from crew_agents import (
    make_reader_agent,
    make_summarizer_agent,
    make_extractor_agent,
    make_mapper_agent,
    make_image_selector_agent,
    make_vision_analyst_agent,
    make_reviewer_agent,
)
from models.schemas import (
    ImageSelection, ImageInsights, ExtractionResult,
    CardManifest, ReviewResult, ReviewedCardManifest,
    ObjectScore, SelectedImage, ImplementationNotes,
)

load_dotenv()


# ── Helpers ────────────────────────────────────────────────────────────────────

def paper_id_from_folder(paper_folder: Path) -> str:
    return paper_folder.name.replace(" ", "_")


def save_output(paper_id: str, step: str, data: dict | str) -> Path:
    out_dir = Path("outputs") / paper_id
    out_dir.mkdir(parents=True, exist_ok=True)
    filepath = out_dir / f"{step}.json"
    content = (
        data if isinstance(data, str)
        else json.dumps(data, ensure_ascii=False, indent=2)
    )
    filepath.write_text(content, encoding="utf-8")
    return filepath


def load_task_prompts() -> dict:
    path = Path(__file__).parent / "prompts" / "tasks.yaml"
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def _load_json_output(paper_id: str, filename: str) -> dict:
    filepath = Path("outputs") / paper_id / filename
    if not filepath.exists():
        raise FileNotFoundError(
            f"Intermediate output not found: {filepath}\n"
            f"Run without --from-step first to generate previous outputs."
        )
    return json.loads(filepath.read_text(encoding="utf-8"))


# ── Logging ────────────────────────────────────────────────────────────────────

def setup_logging(paper_id: str) -> logging.Logger:
    log_dir = Path("outputs") / paper_id
    log_dir.mkdir(parents=True, exist_ok=True)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    log_file  = log_dir / f"session_{timestamp}.log"

    logger = logging.getLogger("paper_cave")
    logger.setLevel(logging.DEBUG)

    fh = logging.FileHandler(log_file, encoding="utf-8")
    fh.setLevel(logging.DEBUG)
    fh.setFormatter(logging.Formatter(
        "%(asctime)s [%(levelname)s] %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    ))

    ch = logging.StreamHandler(sys.stdout)
    ch.setLevel(logging.INFO)
    ch.setFormatter(logging.Formatter("%(message)s"))

    if not logger.handlers:
        logger.addHandler(fh)
        logger.addHandler(ch)
        for lib in ["crewai", "litellm"]:
            logging.getLogger(lib).addHandler(fh)

    logger.info(f"Session log: {log_file}")
    return logger


# ── Post-processing ────────────────────────────────────────────────────────────

def _recover_pydantic(raw: str, model_cls):
    if not raw:
        return None
    try:
        return model_cls.model_validate_json(raw)
    except Exception:
        pass
    cleaned = extract_json_from_output(raw)
    if cleaned == raw:
        return None
    try:
        return model_cls.model_validate_json(cleaned)
    except Exception:
        pass
    return None


def _process_output(task_out, model_cls, logger=None):
    if task_out is None:
        return None, ""
    raw = task_out.raw or ""
    if task_out.pydantic is not None:
        return task_out.pydantic, raw
    if has_thinking_tags(raw) and logger:
        logger.info("  Reasoning tags detected — stripping...")
    recovered = _recover_pydantic(raw, model_cls)
    if recovered is not None and logger:
        logger.info("  Schema recovered after output cleaning.")
    return recovered, raw


# ── Vision support detection ───────────────────────────────────────────────────

def _provider_supports_vision(cfg: dict) -> bool:
    provider = cfg.get("provider", "google")
    model    = cfg.get("model", "").lower()
    vision_providers = {"google"}
    vision_models    = {
        "gpt-4o", "gpt-4o-mini", "gpt-4-vision",
        "claude-opus-4", "claude-sonnet-4", "claude-haiku-4",
        "claude-opus-4-5", "claude-sonnet-4-5",
        "claude-opus-4-7", "claude-sonnet-4-6",
    }
    if provider in vision_providers:
        return True
    if any(vm in model for vm in vision_models):
        return True
    return False


# ── Mapper → Reviewer retry loop ──────────────────────────────────────────────

def _format_attempt_history(history: list[dict]) -> str:
    if not history:
        return "No previous attempts."
    lines = []
    for i, entry in enumerate(history, 1):
        if entry["review"] is None:
            lines.append(f"Attempt {i}: Reviewer failed on schema.")
            continue
        scores = ", ".join(
            f"{s['suggestedName']}={s['score']:.2f}"
            for s in entry["review"]["objectScores"]
        )
        lines.append(f"Attempt {i}: {scores}")
    return "\n".join(lines)


def _run_mapper_reviewer_loop(
    manifest_context: str,
    agents: dict,
    tp: dict,
    available_figures: list[str],
    card_count: int = 5,
    simple_mode: bool = False,
    map_task_base_description: str | None = None,
    max_attempts: int = 3,
    logger=None,
) -> ReviewedCardManifest | None:
    """
    Runs the Mapper → Reviewer loop with attempt history.
    Assembles the best manifest from the highest-scoring cards across attempts.

    Args:
        manifest_context:         The extracted elements JSON to pass to the Mapper.
        card_count:               Number of cards to produce / expect.
        map_task_base_description: Pre-built task description with card_count/figures/
                                   assets/style injected (v4.1). Falls back to
                                   tp["map"]["description"] if None (simple mode/legacy).
        available_figures:        List of FIG* IDs (logged; not stored on the manifest).
    """
    map_task_key = "map_simple" if simple_mode else "map"
    if map_task_base_description and not simple_mode:
        base_desc = map_task_base_description
    else:
        # Simple mode / legacy: inject card_count placeholder ourselves.
        base_desc = tp[map_task_key]["description"].replace("{card_count}", str(card_count))

    expected_output = tp[map_task_key]["expected_output"].replace("{card_count}", str(card_count))

    attempt_history: list[dict] = []
    best_cards: dict[int, tuple] = {}
    last_manifest: CardManifest | None = None

    for attempt in range(1, max_attempts + 1):
        if logger:
            logger.info(f"  Mapper attempt {attempt}/{max_attempts}")

        # Inject Reviewer feedback from previous attempt
        mapper_context = manifest_context
        if attempt > 1 and attempt_history:
            last_review = attempt_history[-1]["review"]
            if last_review is not None:
                feedback_lines = [
                    f"  - {s['suggestedName']}: {s['feedback']}"
                    for s in last_review["objectScores"]
                    if s["score"] < 0.6
                ]
                if feedback_lines:
                    mapper_context += (
                        f"\n\nREVIEWER FEEDBACK FROM ATTEMPT {attempt - 1}:\n"
                        + "\n".join(feedback_lines)
                        + "\n\nFix only the flagged cards. Keep the others unchanged."
                    )

        map_task = Task(
            description=base_desc + f"\n\nExtracted elements:\n{mapper_context}",
            expected_output=expected_output,
            agent=agents["mapper"],
            output_pydantic=CardManifest,
        )
        mapper_result = Crew(
            agents=[agents["mapper"]],
            tasks=[map_task],
            process=Process.sequential,
            verbose=True,
        ).kickoff()

        task_out  = mapper_result.tasks_output[0]
        manifest, _ = _process_output(task_out, CardManifest, logger)

        if manifest is None:
            if logger:
                logger.warning(f"  Mapper attempt {attempt} failed on schema.")
            attempt_history.append({"manifest": None, "review": None})
            continue

        last_manifest = manifest

        # Reviewer
        attempt_history_str = _format_attempt_history(attempt_history)
        review_task = Task(
            description=(
                tp["review"]["description"]
                .replace("{attempt_history}", attempt_history_str)
                .replace("{current_attempt}", str(attempt))
                + f"\n\nManifest to evaluate:\n{manifest.model_dump_json(indent=2)}"
            ),
            expected_output=tp["review"]["expected_output"],
            agent=agents["reviewer"],
            output_pydantic=ReviewResult,
        )
        review_raw = Crew(
            agents=[agents["reviewer"]],
            tasks=[review_task],
            process=Process.sequential,
            verbose=True,
        ).kickoff()

        review, _ = _process_output(review_raw.tasks_output[0], ReviewResult, logger)

        if review is None:
            if logger:
                logger.warning(f"  Reviewer attempt {attempt} failed on schema.")
            attempt_history.append({"manifest": manifest, "review": None})
            break

        attempt_history.append({
            "manifest": manifest,
            "review":   review.model_dump(),
        })

        for idx, (card, score_obj) in enumerate(
            zip(manifest.cards, review.objectScores)
        ):
            if idx not in best_cards or score_obj.score > best_cards[idx][1]:
                best_cards[idx] = (card, score_obj.score)

        if review.approved:
            if logger:
                logger.info(f"  Manifest approved on attempt {attempt}.")
            break

        if attempt == max_attempts and logger:
            logger.info(
                f"  Max attempts reached. "
                f"Delivering best manifest from {max_attempts} attempts."
            )

    if not best_cards:
        return None

    best_card_list = [best_cards[i][0] for i in range(len(best_cards))]
    best_scores = [
        ObjectScore(
            suggestedName=best_cards[i][0].title,
            score=best_cards[i][1],
            confidence=(
                "high"   if best_cards[i][1] >= 0.8 else
                "medium" if best_cards[i][1] >= 0.6 else
                "low"
            ),
            feedback="",
        )
        for i in range(len(best_cards))
    ]

    assembled_from_multiple = len(attempt_history) > 1 and any(
        attempt_history[-1]["review"] is not None
        and best_cards[i][1] != attempt_history[-1]["review"]["objectScores"][i]["score"]
        for i in range(len(best_cards))
    )

    ref_manifest = last_manifest or attempt_history[-1]["manifest"]
    return ReviewedCardManifest(
        paperTitle=ref_manifest.paperTitle,
        centralContribution=ref_manifest.centralContribution,
        cardCount=len(best_card_list),
        cards=best_card_list,
        objectScores=best_scores,
        totalAttempts=len(attempt_history),
        assembledFromMultipleAttempts=assembled_from_multiple,
        implementationNotes=ImplementationNotes(),
    )


# ── Image extraction sub-pipeline ─────────────────────────────────────────────

def _extract_and_select_images(
    pdf_path: str,
    paper_id: str,
    agents: dict,
    tp: dict,
    logger=None,
) -> tuple[ImageSelection | None, ImageInsights | None]:
    try:
        from utils.image_extractor import extract_images_with_context
    except ImportError:
        if logger:
            logger.warning("  image_extractor not available — image step skipped.")
        return None, None

    raw_dir = Path("outputs") / paper_id / "images" / "raw"

    try:
        image_contexts = extract_images_with_context(pdf_path, raw_dir)
    except Exception as e:
        if logger:
            logger.warning(f"  Image extraction failed: {e} — continuing without images.")
        return None, None

    if not image_contexts:
        if logger:
            logger.info("  No images found in PDF.")
        return None, None

    if logger:
        logger.info(f"  {len(image_contexts)} image(s) extracted from PDF.")

    images_summary = "\n\n".join(
        f"[{ctx.raw_filename}] page {ctx.page} | {ctx.width_px}x{ctx.height_px}px\n"
        f"Caption: {ctx.caption or '(no caption)'}\n"
        f"Context before: {ctx.context_before[:200] or '(empty)'}\n"
        f"Context after: {ctx.context_after[:200] or '(empty)'}"
        for ctx in image_contexts
    )

    selector_task = Task(
        description=tp["select_images"]["description"] + f"\n\nAvailable images:\n{images_summary}",
        expected_output=tp["select_images"]["expected_output"],
        agent=agents["image_selector"],
        output_pydantic=ImageSelection,
    )
    selector_result = Crew(
        agents=[agents["image_selector"]],
        tasks=[selector_task],
        process=Process.sequential,
        verbose=True,
    ).kickoff()

    selection, _ = _process_output(selector_result.tasks_output[0], ImageSelection, logger)

    if selection is None or not selection.selected:
        if logger:
            logger.warning("  Image Selector returned no valid images.")
        return None, None

    images_dir = Path("outputs") / paper_id / "images"
    images_dir.mkdir(parents=True, exist_ok=True)
    for sel in selection.selected:
        src = raw_dir / sel.raw_filename
        dst = images_dir / sel.filename
        if src.exists():
            shutil.copy2(src, dst)

    if logger:
        logger.info(f"  {len(selection.selected)} image(s) selected.")

    analyst_context = "\n\n".join(
        f"[{sel.filename}]\nCaption: {sel.caption or '(no caption)'}\n"
        f"Selection justification: {sel.selection_justification}"
        for sel in selection.selected
    )

    analyst_task = Task(
        description=tp["analyze_images"]["description"] + f"\n\nSelected images:\n{analyst_context}",
        expected_output=tp["analyze_images"]["expected_output"],
        agent=agents["vision_analyst"],
        output_pydantic=ImageInsights,
    )
    analyst_result = Crew(
        agents=[agents["vision_analyst"]],
        tasks=[analyst_task],
        process=Process.sequential,
        verbose=True,
    ).kickoff()

    insights, _ = _process_output(analyst_result.tasks_output[0], ImageInsights, logger)

    if logger and insights:
        logger.info(f"  Vision Analyst processed {len(insights.insights)} insight(s).")

    return selection, insights


# ── Main pipeline ──────────────────────────────────────────────────────────────

def run(
    pdf_path: str,
    paper_folder: Path,
    from_step: str | None = None,
    simple_mode: bool = False,
):
    cfg      = load_config()
    paper_id = paper_id_from_folder(paper_folder)
    ts       = datetime.now().strftime("%Y%m%d_%H%M%S")

    logger = setup_logging(paper_id)
    mode_label = " [SIMPLE MODE]" if simple_mode else ""
    logger.info(f"\n{'='*60}")
    logger.info(f"  Paper Cave v4.1{mode_label} — {paper_id}")
    logger.info(f"  Provider: {get_config_summary(cfg)}")
    logger.info(f"  {ts}")
    if from_step:
        logger.info(f"  Mode: resuming from '{from_step}'")
    logger.info(f"{'='*60}\n")

    max_retries = cfg.get("max_schema_retries", 3)
    card_count  = cfg.get("card_count", 5)

    # ── LLMs ──────────────────────────────────────────────────────────────────
    llm_plain  = make_llm(cfg)
    llm_json   = make_json_llm(cfg)
    has_vision = _provider_supports_vision(cfg)

    vision_label = "VISUAL mode" if has_vision else "TEXT INFERRED mode"
    logger.info(f"  Vision Analyst: {vision_label}")

    # ── Load paper context (figures, catalog, style guide) ─────────────────────
    paper_context = load_paper_context(paper_folder)
    available_figs = paper_context["available_figures"]
    if available_figs:
        logger.info(f"  Paper figures found: {', '.join(available_figs)}")
    else:
        logger.info("  No FIG*.png figures found in paper folder.")

    # ── Extract PDF text for RAG and context checking ──────────────────────────
    full_text   = ""
    search_tool = None

    needs_pdf_read = from_step in (None, "image_selector", "vision_analyst", "summarizer")

    if needs_pdf_read and Path(pdf_path).exists():
        try:
            import fitz
            doc       = fitz.open(pdf_path)
            full_text = "\n\n".join(page.get_text() for page in doc)
            doc.close()
            should_proceed = check_and_warn(
                text=full_text,
                model=cfg.get("model", ""),
                base_url=cfg.get("base_url"),
                cfg=cfg,
            )
            if not should_proceed:
                sys.exit(0)
        except Exception as e:
            logger.warning(f"Could not verify PDF context: {e}")

    if not full_text and Path(pdf_path).exists():
        try:
            import fitz
            doc       = fitz.open(pdf_path)
            full_text = "\n\n".join(page.get_text() for page in doc)
            doc.close()
        except Exception:
            pass

    # Populate figure captions from PDF text
    if full_text and available_figs:
        update_figure_captions(paper_context, full_text)
        logger.info(
            f"  Figure captions extracted: "
            f"{len(paper_context['figure_captions'])}/{len(available_figs)}"
        )

    # Build RAG index
    if full_text:
        try:
            from utils.rag_indexer import build_paper_search_tool
            search_tool = build_paper_search_tool(full_text, cfg)
        except (Exception, KeyboardInterrupt) as e:
            if isinstance(e, KeyboardInterrupt):
                logger.warning("RAG interrupted — continuing without search.")
            else:
                logger.warning(f"RAG disabled: {e}")
            search_tool = None

    # ── Build map task description with injected context (v4) ─────────────────
    tp = load_task_prompts()
    map_task_description = None
    if not simple_mode:
        map_task_description = _build_map_task_description(tp, paper_context, card_count)

    # ── Instantiate agents ─────────────────────────────────────────────────────
    agents = {
        "reader":         make_reader_agent(llm_plain),
        "image_selector": make_image_selector_agent(llm_json),
        "vision_analyst": make_vision_analyst_agent(llm_plain),
        "summarizer":     make_summarizer_agent(llm_plain),
        "extractor":      make_extractor_agent(llm_json, max_retries=max_retries),
        "mapper":         make_mapper_agent(
                              llm_plain,
                              search_tool=search_tool,
                              max_retries=max_retries,
                              simple_mode=simple_mode,
                              card_count=card_count,
                          ),
        "reviewer":       make_reviewer_agent(llm_plain, max_retries=max_retries),
    }

    selection: ImageSelection | None = None
    insights:  ImageInsights  | None = None
    summary_text = ""
    extraction:  ExtractionResult | None = None

    # ── Step 1: Reader ─────────────────────────────────────────────────────────
    if from_step is None:
        logger.info("  [1/7] Reader — extracting text from PDF...")
        read_task = Task(
            description=tp["read"]["description"].format(pdf_path=pdf_path),
            expected_output=tp["read"]["expected_output"],
            agent=agents["reader"],
        )
        read_result = Crew(
            agents=[agents["reader"]],
            tasks=[read_task],
            process=Process.sequential,
            verbose=True,
        ).kickoff()
        reader_out  = read_result.tasks_output[0]
        raw_text    = reader_out.raw or ""
        text_clean  = strip_thinking_tags(raw_text) if has_thinking_tags(raw_text) else raw_text
        save_output(paper_id, "01_reader_output", {"raw": text_clean})
        full_text   = text_clean or full_text

        # Update captions with Reader's cleaned text (more reliable than pre-extraction)
        if available_figs and full_text:
            update_figure_captions(paper_context, full_text)
    else:
        try:
            full_text = _load_json_output(paper_id, "01_reader_output.json").get("raw", full_text)
            if available_figs and full_text:
                update_figure_captions(paper_context, full_text)
        except FileNotFoundError:
            pass

    # Rebuild map task description with updated captions after Reader
    if not simple_mode and full_text:
        map_task_description = _build_map_task_description(tp, paper_context, card_count)

    # ── Step 2: Image Selector + Vision Analyst ────────────────────────────────
    skip_images = from_step in ("summarizer", "extractor", "mapper", "reviewer")

    if not skip_images:
        if from_step in (None, "image_selector", "vision_analyst"):
            logger.info("  [2/7] Image Selector + Vision Analyst...")
            selection, insights = _extract_and_select_images(
                pdf_path, paper_id, agents, tp, logger
            )
            if selection:
                save_output(paper_id, "02_image_selection", selection.model_dump())
            if insights:
                save_output(paper_id, "03_vision_insights", insights.model_dump())
    else:
        try:
            sel_data  = _load_json_output(paper_id, "02_image_selection.json")
            selection = ImageSelection.model_validate(sel_data)
        except (FileNotFoundError, Exception):
            pass
        try:
            ins_data  = _load_json_output(paper_id, "03_vision_insights.json")
            insights  = ImageInsights.model_validate(ins_data)
        except (FileNotFoundError, Exception):
            pass

    # ── Step 3: Summarizer ─────────────────────────────────────────────────────
    if from_step not in ("extractor", "mapper", "reviewer"):
        logger.info("  [4/7] Summarizer...")

        visual_block = ""
        if insights and insights.insights:
            lines = ["VISUAL INSIGHTS:"]
            for ins in insights.insights:
                lines.append(
                    f"  [{ins.filename}] {ins.description} "
                    f"(relevance: {ins.relevance})"
                    + (" [text inferred]" if ins.mode == "text_inferred" else "")
                )
            visual_block = "\n" + "\n".join(lines)

        summarize_desc = (
            tp["summarize"]["description"]
            + f"\n\nFull paper text:\n\n{full_text[:60000]}"
            + visual_block
        )
        summarize_task = Task(
            description=summarize_desc,
            expected_output=tp["summarize"]["expected_output"],
            agent=agents["summarizer"],
        )
        sum_result = Crew(
            agents=[agents["summarizer"]],
            tasks=[summarize_task],
            process=Process.sequential,
            verbose=True,
        ).kickoff()
        sum_out   = sum_result.tasks_output[0]
        raw_sum   = sum_out.raw or ""
        sum_clean = strip_thinking_tags(raw_sum) if has_thinking_tags(raw_sum) else raw_sum
        save_output(paper_id, "04_summarizer_output", {"raw": sum_clean})
        summary_text = sum_clean
    else:
        summary_text = _load_json_output(paper_id, "04_summarizer_output.json").get("raw", "")

    # ── Step 4: Extractor ──────────────────────────────────────────────────────
    if from_step not in ("mapper", "reviewer"):
        logger.info("  [5/7] Extractor...")
        extract_task = Task(
            description=tp["extract"]["description"] + f"\n\nSummary:\n\n{summary_text}",
            expected_output=tp["extract"]["expected_output"],
            agent=agents["extractor"],
            output_pydantic=ExtractionResult,
        )
        ext_result = Crew(
            agents=[agents["extractor"]],
            tasks=[extract_task],
            process=Process.sequential,
            verbose=True,
        ).kickoff()
        extraction, _ = _process_output(ext_result.tasks_output[0], ExtractionResult, logger)

        if extraction is None:
            logger.error("  ERROR: Extractor failed — aborting pipeline.")
            return None
        save_output(paper_id, "05_extractor_output", extraction.model_dump())
    else:
        ext_data   = _load_json_output(paper_id, "05_extractor_output.json")
        extraction = ExtractionResult.model_validate(ext_data)

    # ── Steps 5+6: Mapper → Reviewer (retry loop) ─────────────────────────────
    if from_step != "reviewer":
        logger.info("  [6-7/7] Mapper → Reviewer (retry loop)...")

        # Build manifest context: extraction result + selected images (if any)
        images_ref = ""
        if selection and selection.selected:
            images_ref = "\n\nSelected images available for assetReference:\n" + "\n".join(
                f"  - {s.filename}: {s.caption or s.selection_justification}"
                for s in selection.selected
            )

        manifest_context = extraction.model_dump_json(indent=2) + images_ref

        reviewed = _run_mapper_reviewer_loop(
            manifest_context=manifest_context,
            agents=agents,
            tp=tp,
            available_figures=available_figs,
            card_count=card_count,
            simple_mode=simple_mode,
            map_task_base_description=map_task_description,
            max_attempts=max_retries,
            logger=logger,
        )

        if reviewed is None:
            logger.error("  ERROR: Mapper/Reviewer failed on all attempts.")
            return None

        save_output(paper_id, "06_mapper_output",   reviewed.model_dump())
        save_output(paper_id, "07_reviewer_output", reviewed.model_dump())
    else:
        logger.info("  [7/7] Reviewer — re-evaluating saved manifest...")

        manifest_context = extraction.model_dump_json(indent=2)
        reviewed = _run_mapper_reviewer_loop(
            manifest_context=manifest_context,
            agents=agents,
            tp=tp,
            available_figures=available_figs,
            card_count=card_count,
            simple_mode=simple_mode,
            map_task_base_description=map_task_description,
            max_attempts=max_retries,
            logger=logger,
        )
        if reviewed:
            save_output(paper_id, "07_reviewer_output", reviewed.model_dump())

    logger.info(f"\n{'='*60}")
    logger.info(f"  Done. Outputs at: outputs/{paper_id}/")
    if simple_mode:
        logger.info("  Mode: SIMPLE (figure/chart cards only)")
    if available_figs:
        logger.info(f"  Paper figures referenced: {', '.join(available_figs)}")
    logger.info(f"{'='*60}\n")

    return reviewed


# ── Entry point ────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Paper Cave v4.1 — transforms papers into Card Manifests for Unity.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python crew.py                                      # interactive folder selector
  python crew.py --paper papers/my_paper/            # specify folder directly
  python crew.py --simple                             # Simple Mode (Chart/Image/Table/Text)
  python crew.py --from-step extractor               # resume from Extractor
  python crew.py --from-step mapper                  # resume from Mapper
  python crew.py --from-step reviewer                # resume from Reviewer

Paper folder structure:
  papers/
    my_paper/
      paper.pdf       <- the PDF
      FIG1.png        <- figure 1 (optional but recommended)
      FIG2.png        <- figure 2
        """,
    )
    parser.add_argument(
        "--paper",
        help="Path to the paper folder (e.g., papers/my_paper/). If omitted, opens interactive selector.",
        default=None,
    )
    parser.add_argument(
        "--from-step",
        choices=[
            "image_selector", "vision_analyst", "summarizer",
            "extractor", "mapper", "reviewer",
        ],
        help="Resume execution from this step, reusing previous outputs.",
        default=None,
    )
    parser.add_argument(
        "--simple",
        action="store_true",
        help="Simple Mode: cards restricted to contentType figure/chart (no animation).",
    )
    args = parser.parse_args()

    if args.paper:
        folder   = Path(args.paper)
        pdfs     = list(folder.glob("*.pdf"))
        if not pdfs:
            print(f"  No PDF found in {folder}")
            sys.exit(1)
        pdf_path    = str(pdfs[0])
        paper_folder = folder
    else:
        pdf_path, paper_folder = select_paper_folder()

    run(
        pdf_path=pdf_path,
        paper_folder=paper_folder,
        from_step=args.from_step,
        simple_mode=args.simple,
    )
