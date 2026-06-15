"""
utils/thinking_stripper.py
Remove tags de raciocínio (thinking/reasoning) de outputs de modelos que as produzem.

Modelos afetados:
- DeepSeek R1, R1-Distill: <think>...</think>
- Qwen QwQ, Qwen3 (thinking mode): <think>...</think>
- Alguns modelos Ollama em modo COT: <thinking>...</thinking> ou <reasoning>...</reasoning>

Para Claude Extended Thinking (via API Anthropic), o pensamento é retornado
em blocos de conteúdo separados — não aparece no texto, então não precisa de tratamento.
Para modelos GPT/Gemini padrão, não há tags de thinking.
"""
import re


# Padrões de tags de raciocínio conhecidos
_THINKING_PATTERNS = [
    re.compile(r"<think>.*?</think>",       re.DOTALL | re.IGNORECASE),
    re.compile(r"<thinking>.*?</thinking>", re.DOTALL | re.IGNORECASE),
    re.compile(r"<reasoning>.*?</reasoning>", re.DOTALL | re.IGNORECASE),
    re.compile(r"<reflection>.*?</reflection>", re.DOTALL | re.IGNORECASE),
]

# Detecta se o output provavelmente veio de um modelo com raciocínio ativo
_THINKING_DETECT = re.compile(
    r"<think>|<thinking>|<reasoning>|<reflection>",
    re.IGNORECASE,
)


def has_thinking_tags(text: str) -> bool:
    """Retorna True se o texto contiver tags de raciocínio."""
    return bool(_THINKING_DETECT.search(text))


def strip_thinking_tags(text: str) -> str:
    """
    Remove blocos de raciocínio do texto e retorna apenas o conteúdo útil.
    Aplica todos os padrões conhecidos e limpa espaços residuais.
    """
    for pattern in _THINKING_PATTERNS:
        text = pattern.sub("", text)
    # Remove linhas em branco excessivas deixadas pela remoção
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def extract_json_from_output(text: str) -> str:
    """
    Extrai o conteúdo JSON de um output que pode conter:
    - Tags de thinking antes do JSON
    - Markdown code fences (```json ... ```)
    - Texto livre antes/depois do JSON

    Retorna a string JSON mais provável, ou o texto original se nada for encontrado.
    """
    # 1. Remover tags de thinking primeiro
    cleaned = strip_thinking_tags(text)

    # 2. Tentar extrair de code fences ```json ... ```
    fence_match = re.search(r"```(?:json)?\s*(\{.*?\}|\[.*?\])\s*```", cleaned, re.DOTALL)
    if fence_match:
        return fence_match.group(1).strip()

    # 3. Encontrar o bloco JSON mais externo (primeiro { ... } ou [ ... ] completo)
    for start_char, end_char in [("{", "}"), ("[", "]")]:
        start = cleaned.find(start_char)
        if start == -1:
            continue
        depth = 0
        in_string = False
        escape_next = False
        for i, ch in enumerate(cleaned[start:], start):
            if escape_next:
                escape_next = False
                continue
            if ch == "\\" and in_string:
                escape_next = True
                continue
            if ch == '"' and not escape_next:
                in_string = not in_string
            if not in_string:
                if ch == start_char:
                    depth += 1
                elif ch == end_char:
                    depth -= 1
                    if depth == 0:
                        return cleaned[start : i + 1]

    # 4. Retornar texto limpo como fallback
    return cleaned
