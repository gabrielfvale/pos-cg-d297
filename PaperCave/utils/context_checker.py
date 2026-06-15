"""
utils/context_checker.py
Detecta janela de contexto do modelo configurado e estima tokens do paper.
Suporta: modelos conhecidos via litellm, LMStudio via API nativa, modelos desconhecidos.
"""
import re
import requests
import litellm
from typing import Optional


CONTEXT_SAFETY_FACTOR = 0.70
CHARS_PER_TOKEN = 4
UNKNOWN_MODEL_CONTEXT = 8192


def estimate_tokens(text: str) -> int:
    """Estimativa rápida de tokens sem depender de tokenizer específico."""
    return max(1, len(text) // CHARS_PER_TOKEN)


def get_context_window(model: str, base_url: Optional[str] = None) -> Optional[int]:
    """
    Tenta obter a janela de contexto do modelo.
    1. LMStudio: chama /api/v1/models para obter max_context
    2. Modelo conhecido: usa litellm.get_model_info
    3. Desconhecido: retorna None
    """
    if base_url and "localhost" in base_url:
        lmstudio_context = _get_lmstudio_context(base_url)
        if lmstudio_context:
            return lmstudio_context

    try:
        info = litellm.get_model_info(model)
        return info.get("max_input_tokens") or info.get("max_tokens")
    except Exception:
        return None


def _get_lmstudio_context(base_url: str) -> Optional[int]:
    """Chama o endpoint nativo do LMStudio (/api/v1/models) para obter max_context."""
    try:
        base = re.sub(r"/v1/?$", "", base_url.rstrip("/"))
        url = f"{base}/api/v1/models"
        resp = requests.get(url, timeout=3)
        if resp.status_code == 200:
            data = resp.json()
            models = data.get("data", [])
            if models:
                return models[0].get("max_context_length") or models[0].get("max_context")
    except Exception:
        pass
    return None


def check_and_warn(text: str, model: str, base_url: Optional[str], cfg: dict) -> bool:
    """
    Verifica se o paper cabe na janela de contexto do modelo.
    Retorna True se deve prosseguir, False se o usuário cancelou.
    """
    token_estimate = estimate_tokens(text)
    context_window = get_context_window(model, base_url)
    confirm = cfg.get("confirm_large_papers", True)

    print(f"\n  Tamanho estimado do paper: ~{token_estimate:,} tokens")

    if context_window:
        safe_limit = int(context_window * CONTEXT_SAFETY_FACTOR)
        print(f"  Janela de contexto do modelo: {context_window:,} tokens")
        print(f"  Limite seguro (70%): {safe_limit:,} tokens")

        if token_estimate > safe_limit:
            print(f"\n  AVISO: O paper excede o limite seguro do modelo.")
            print(f"         Chunking automático será aplicado no Summarizer.")
            if confirm:
                resp = input("  Deseja prosseguir? [S/n] ").strip().lower()
                if resp == "n":
                    print("  Execução cancelada pelo usuário.")
                    return False
        else:
            print(f"  OK: Paper dentro do limite seguro. Processando normalmente.")
    else:
        print(f"  AVISO: Modelo desconhecido — janela de contexto não detectada.")
        print(f"         Estimativa: ~{token_estimate:,} tokens.")
        if confirm:
            print(f"         Se o modelo tiver janela pequena (ex: < 8k), o processo pode falhar.")
            resp = input("  Deseja prosseguir? [S/n] ").strip().lower()
            if resp == "n":
                print("  Execução cancelada pelo usuário.")
                return False

    return True
