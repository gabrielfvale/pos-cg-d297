"""
utils/image_extractor.py
Canonical figure extraction from academic paper PDFs.

Extracts only *real* figures — those with a nearby "Fig N / Figure N / Figura N"
caption — discarding logos, decorations, and other non-figure images.

Multi-line captions are handled by collecting all text blocks within a spatial
window around the image, joining their text, and running the caption regex over
the joined string.  Two-column layouts are supported via position-based block
selection (x-overlap filtering).

Public API:
  extract_figures_from_pdf(paper_folder, pdf_path, ...)
    → dict[str, str]   {fig_number: caption_text}
    Also writes FIG_<N>.png and captions.txt to paper_folder.
    Clears existing FIG_*.png before writing (no stale files from failed runs).
"""
import re
import fitz  # pymupdf
from pathlib import Path

MIN_WIDTH  = 80
MIN_HEIGHT = 80
CAPTION_BELOW_PT  = 150   # points below image bottom to search for caption
CAPTION_ABOVE_PT  = 60    # points above image top (caption-before-figure style)
X_MARGIN          = 30    # horizontal tolerance for block–image alignment
MAX_CAPTION_CHARS = 500   # truncate very long captions

_CAPTION_START = re.compile(
    r"(Fig\.?\s*(\d+[a-zA-Z]?)"
    r"|Figure\s+(\d+[a-zA-Z]?)"
    r"|Figura\s+(\d+[a-zA-Z]?))",
    re.IGNORECASE,
)


# ── Spatial helpers ────────────────────────────────────────────────────────────

def _x_overlaps(bx0: float, bx1: float, ix0: float, ix1: float) -> bool:
    return bx1 > ix0 - X_MARGIN and bx0 < ix1 + X_MARGIN


def _blocks_in_band(
    blocks: list,
    y_min: float, y_max: float,
    ix0: float, ix1: float,
) -> list:
    return sorted(
        [b for b in blocks
         if b[1] >= y_min and b[1] <= y_max
         and _x_overlaps(b[0], b[2], ix0, ix1)],
        key=lambda b: b[1],
    )


# ── Caption extraction ─────────────────────────────────────────────────────────

def _caption_from_blocks(blocks: list) -> tuple[str, str]:
    """
    Join text from all blocks and find a 'Fig N' caption.
    Joining handles multi-line captions that span several PyMuPDF text blocks.
    Returns (fig_number_str, full_caption_text) or ('', '').
    """
    if not blocks:
        return "", ""
    joined = " ".join(b[4].replace("\n", " ").strip() for b in blocks)
    joined = re.sub(r"\s{2,}", " ", joined)
    m = _CAPTION_START.search(joined)
    if not m:
        return "", ""
    num = (m.group(2) or m.group(3) or m.group(4) or "").upper()
    caption = joined[m.start(): m.start() + MAX_CAPTION_CHARS].strip()
    return num, caption


def _find_caption(img_rect: fitz.Rect, text_blocks: list) -> tuple[str, str]:
    """
    Find the caption for an image using its bounding box.
    Searches below first (most journals), then above (caption-before-figure style).
    Returns (fig_number, caption_text) or ('', '') if no caption found.
    """
    iy0, iy1 = img_rect.y0, img_rect.y1
    ix0, ix1 = img_rect.x0, img_rect.x1

    below = _blocks_in_band(text_blocks, iy1 - 5, iy1 + CAPTION_BELOW_PT, ix0, ix1)
    num, cap = _caption_from_blocks(below)
    if num:
        return num, cap

    above = _blocks_in_band(text_blocks, iy0 - CAPTION_ABOVE_PT, iy0 + 5, ix0, ix1)
    above = sorted(above, key=lambda b: -b[1])   # closest-first
    return _caption_from_blocks(above)


# ── Cleanup ────────────────────────────────────────────────────────────────────

def _clear_figures(paper_folder: Path, verbose: bool) -> None:
    """Remove all FIG_*.png and captions.txt from the paper folder."""
    to_remove = set(paper_folder.glob("FIG*.png"))
    if verbose and to_remove:
        print(f"    Removendo {len(to_remove)} FIG*.png anteriores.")
    for f in to_remove:
        f.unlink()
    cap = paper_folder / "captions.txt"
    if cap.exists():
        cap.unlink()


# ── Main public function ───────────────────────────────────────────────────────

def extract_figures_from_pdf(
    paper_folder: Path,
    pdf_path: Path,
    min_width: int = MIN_WIDTH,
    min_height: int = MIN_HEIGHT,
    verbose: bool = True,
) -> dict[str, str]:
    """
    Extract real figures from a PDF (only those with a nearby 'Fig N' caption).

    Saves to paper_folder:
      - FIG_<N>.png  named by actual figure number in the paper
      - captions.txt  one caption per figure

    Clears existing FIG_*.png before writing — no stale files from failed runs.

    Returns dict mapping fig_number string → caption text.
    """
    _clear_figures(paper_folder, verbose)

    doc = fitz.open(str(pdf_path))
    seen_xrefs: set[int] = set()
    used_numbers: set[str] = set()
    captions: dict[str, str] = {}

    for page_num, page in enumerate(doc, 1):
        text_blocks = [
            (b[0], b[1], b[2], b[3], b[4])
            for b in page.get_text("blocks")
            if b[6] == 0   # text blocks only (not image blocks)
        ]

        for img_ref in page.get_images(full=True):
            xref = img_ref[0]
            if xref in seen_xrefs:
                continue
            seen_xrefs.add(xref)

            try:
                base_image = doc.extract_image(xref)
            except Exception:
                continue

            w, h = base_image["width"], base_image["height"]
            if w < min_width or h < min_height:
                continue

            rects = page.get_image_rects(xref)
            if not rects:
                continue

            fig_num, caption = _find_caption(rects[0], text_blocks)
            if not fig_num:
                continue   # no caption found — not a paper figure
            if fig_num in used_numbers:
                continue   # duplicate (same figure number already saved)
            used_numbers.add(fig_num)

            dest = paper_folder / f"FIG_{fig_num}.png"
            dest.write_bytes(base_image["image"])
            captions[fig_num] = caption

            if verbose:
                short = caption[:72].replace("\n", " ")
                print(f"    FIG_{fig_num}.png  ({w}×{h}px, pág.{page_num})  {short}")

    doc.close()

    if captions:
        lines = []
        for num in sorted(captions, key=lambda x: (len(x), x)):
            lines.append(f"FIG_{num}.png")
            lines.append(captions[num])
            lines.append("")
        (paper_folder / "captions.txt").write_text(
            "\n".join(lines), encoding="utf-8"
        )

    if verbose:
        print(f"    Total: {len(captions)} figura(s) extraída(s).")

    return captions


# ── Legacy shim (kept so old imports don't break) ─────────────────────────────

def load_image_bytes(raw_dir: Path, raw_filename: str) -> bytes:
    return (raw_dir / raw_filename).read_bytes()
