"""
utils/step_resume.py
Gerencia retomada de execuções parciais via --from-step.
Carrega outputs intermediários já salvos em outputs/{paper_id}/.
"""
import json
from pathlib import Path
from models.schemas import PaperProfile, DesignPlan


STEP_FILES = {
    "summarizer": "02_summarizer_output.json",
    "classifier": "03_classifier_output.json",
    "designer":   "04_designer_output.json",
}


def load_intermediate(paper_id: str, from_step: str) -> dict:
    """
    Carrega outputs intermediários a partir do step especificado.
    Retorna dict com chaves 'summary', 'profile', 'design' preenchidas
    até o ponto de retomada.
    """
    out_dir = Path("outputs") / paper_id
    result = {}

    steps_before = _steps_before(from_step)
    for step in steps_before:
        filepath = out_dir / STEP_FILES[step]
        if not filepath.exists():
            raise FileNotFoundError(
                f"Output intermediário não encontrado: {filepath}\n"
                f"Execute sem --from-step primeiro para gerar os outputs."
            )
        data = json.loads(filepath.read_text(encoding="utf-8"))

        if step == "summarizer":
            result["summary"] = data.get("raw", "")
        elif step == "classifier":
            result["profile"] = PaperProfile(**data)
        elif step == "designer":
            result["design"] = DesignPlan(**data)

    return result


def _steps_before(from_step: str) -> list[str]:
    order = ["summarizer", "classifier", "designer"]
    try:
        idx = order.index(from_step)
        return order[:idx]
    except ValueError:
        raise ValueError(
            f"--from-step inválido: '{from_step}'. "
            f"Opções: {', '.join(order)}"
        )
