# Paper Cave — Documento de Refatoração

Este documento descreve todas as mudanças a serem implementadas no repositório
do projeto antes de compartilhar com a equipe. Cada seção contém o **porquê**
da mudança, a **especificação exata** do que deve ser feito, e onde necessário
os **novos conteúdos completos** de arquivos.

---

## Índice

1. Reframing conceitual — experiência interativa, não jogo
2. Nova estrutura de pastas
3. Sistema de configuração multi-provider (config.yaml)
4. Suporte a LMStudio e modelos locais
5. Prompts externalizados em YAML
6. Novos prompts completos dos agentes
7. Detecção de janela de contexto e chunking adaptativo
8. Interface de seleção de PDF e estimativa de tokens
9. Sistema de logging completo
10. Flag --from-step para retomar execuções parciais
11. Tratamento de erros de schema (modelos locais)
12. Atualização dos schemas Pydantic
13. crew.py refatorado
14. Referências técnicas

---

## 1. Reframing conceitual — experiência interativa, não jogo

### O problema

Os prompts atuais usam linguagem de game design que psicologicamente empurra
o Designer a criar jogos em vez de experiências interativas. Termos como
"demo jogável", "jogador", "mecânica de jogo" e "objetivo de jogo" induzem
o agente a priorizar entretenimento sobre comunicação de conhecimento.

A máxima correta do projeto é:
> "Qual é a contribuição mais importante desse paper? Como apresentar ela
> de forma interativa para que o usuário, ao navegar pela experiência,
> entenda de modo mais intuitivo do que lendo o paper?"

Experiências interativas podem incluir jogos quando o paper trata de jogos,
mas a estrutura padrão é uma **experiência educacional interativa em Unity**
— equivalente a um museu virtual, instalação interativa ou tour guiado, não
a um arcade.

### O que muda

- "jogador" → "usuário" em todos os prompts e schemas
- "demo jogável" / "demo interativa" → "experiência interativa"
- "mecânica de jogo" → "mecânica de interação"
- "objetivo de jogo" → "etapa da experiência"
- "completionTrigger" mantém o nome técnico mas sua descrição muda de
  "conquista de objetivo" para "condição de avanço da experiência"
- `HealItem` é removido do catálogo de building blocks (é o único objeto
  puramente game-specific sem equivalente em experiências educacionais)
- O narrativeFrame passa a ser descrito como "roteiro da experiência" não
  como "loop de gameplay"

### O que NÃO muda

A arquitetura de navegação em primeira/terceira pessoa em ambiente 3D é
apropriada tanto para jogos quanto para experiências interativas — museus
virtuais, tours educacionais e instalações interativas usam exatamente esse
modelo. Os 5 templates, os building blocks restantes e o schema JSON se
mantêm, apenas com linguagem ajustada.

---

## 2. Nova estrutura de pastas

```
paper_cave/
├── config/
│   ├── config.yaml          # configuração de provider/modelo (editável pela equipe)
│   └── config.example.yaml  # template com todos os exemplos de provider
├── prompts/
│   ├── agents.yaml          # backstory e goal de cada agente
│   └── tasks.yaml           # descrição e expected_output de cada task
├── crew_agents/
│   ├── __init__.py
│   ├── reader.py
│   ├── summarizer.py
│   ├── classifier.py
│   └── designer.py
├── models/
│   ├── __init__.py
│   └── schemas.py
├── tools/
│   ├── __init__.py
│   └── pdf_tool.py
├── utils/
│   ├── __init__.py
│   ├── config_loader.py     # carrega config.yaml e resolve provider/modelo
│   ├── context_checker.py   # detecta janela de contexto e estima tokens
│   ├── pdf_selector.py      # interface de seleção de PDF no terminal
│   └── step_resume.py       # lógica de retomada por --from-step
├── outputs/                 # gerado automaticamente
├── papers/                  # PDFs aqui
├── logs/                    # logs de sessão
├── crew.py                  # entrada principal
├── requirements.txt
├── .env.example
└── README.md
```

---

## 3. Sistema de configuração multi-provider (config.yaml)

### config/config.example.yaml — conteúdo completo

```yaml
# Paper Cave — Configuração de Provider e Modelo
# Copie este arquivo para config/config.yaml e preencha os valores.
# Apenas config.yaml é lido pelo sistema. config.example.yaml é só referência.

# ── Provider e modelo ──────────────────────────────────────────────────────
# Exemplos válidos de provider/model:

# Google Gemini (recomendado para a PoC):
provider: google
model: gemini-2.5-flash

# OpenAI:
# provider: openai
# model: gpt-4o-mini

# Anthropic Claude:
# provider: anthropic
# model: claude-sonnet-4-5

# LMStudio (modelo local):
# provider: lmstudio
# model: openai/nome-exato-do-modelo-carregado
# base_url: http://localhost:1234/v1

# Ollama (modelo local via Ollama):
# provider: ollama
# model: ollama/llama3.2
# base_url: http://localhost:11434

# Qualquer endpoint OpenAI-compatible:
# provider: openai_compatible
# model: openai/nome-do-modelo
# base_url: https://seu-endpoint/v1

# ── Chaves de API ──────────────────────────────────────────────────────────
# Prefira definir chaves no .env (GEMINI_API_KEY, OPENAI_API_KEY, etc.)
# Use api_key aqui apenas se não quiser usar variáveis de ambiente.
# api_key: sua_chave_aqui  # descomente se necessário

# ── Comportamento do sistema ───────────────────────────────────────────────
# Tamanho máximo de chunk em caracteres quando o paper excede a janela de contexto.
# Reduzir para modelos com janela menor. Padrão: 60000 (aprox. 15k tokens).
chunk_size: 60000

# Se true, o sistema pergunta confirmação antes de processar papers grandes.
# Se false, processa automaticamente aplicando chunking quando necessário.
confirm_large_papers: true

# Número máximo de tentativas quando o modelo não produz JSON válido.
max_schema_retries: 3
```

### config/config.yaml — não incluir no repositório

Adicionar ao `.gitignore`:
```
config/config.yaml
.env
```

### utils/config_loader.py — conteúdo completo

```python
"""
utils/config_loader.py
Carrega config/config.yaml e resolve o LLM correto para o provider configurado.
Suporta: google, openai, anthropic, lmstudio, ollama, openai_compatible.
"""
import os
import yaml
from pathlib import Path
from crewai import LLM
from dotenv import load_dotenv

load_dotenv()

CONFIG_PATH = Path(__file__).parent.parent / "config" / "config.yaml"

PROVIDER_NATIVE = {"google", "openai", "anthropic"}

def load_config() -> dict:
    if not CONFIG_PATH.exists():
        raise FileNotFoundError(
            f"config/config.yaml não encontrado. "
            f"Copie config/config.example.yaml para config/config.yaml e configure."
        )
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        return yaml.safe_load(f)

def make_llm(cfg: dict | None = None) -> LLM:
    if cfg is None:
        cfg = load_config()

    provider  = cfg.get("provider", "google")
    model     = cfg.get("model", "gemini-2.5-flash")
    base_url  = cfg.get("base_url", None)
    api_key   = cfg.get("api_key", None)

    # Resolve api_key: config.yaml > .env por provider
    if not api_key:
        env_map = {
            "google":           "GEMINI_API_KEY",
            "openai":           "OPENAI_API_KEY",
            "anthropic":        "ANTHROPIC_API_KEY",
            "lmstudio":         None,
            "ollama":           None,
            "openai_compatible": "OPENAI_API_KEY",
        }
        env_var = env_map.get(provider)
        if env_var:
            api_key = os.getenv(env_var)

    # Providers nativos do CrewAI: não precisam de base_url
    if provider in PROVIDER_NATIVE:
        kwargs = {"model": model}
        if api_key:
            kwargs["api_key"] = api_key
        return LLM(**kwargs)

    # Providers via endpoint OpenAI-compatible (lmstudio, ollama, openai_compatible)
    if not base_url:
        defaults = {
            "lmstudio": "http://localhost:1234/v1",
            "ollama":   "http://localhost:11434",
        }
        base_url = defaults.get(provider, "http://localhost:1234/v1")

    return LLM(
        model=model,
        base_url=base_url,
        api_key=api_key or "local",  # LMStudio e Ollama ignoram a chave
    )

def get_config_summary(cfg: dict) -> str:
    provider = cfg.get("provider", "google")
    model    = cfg.get("model", "gemini-2.5-flash")
    base_url = cfg.get("base_url", "")
    suffix   = f" @ {base_url}" if base_url else ""
    return f"{provider}/{model}{suffix}"
```

---

## 4. Suporte a LMStudio e modelos locais

### Detalhes técnicos do LMStudio

O LMStudio 0.4.0+ expõe dois grupos de endpoints:

1. **OpenAI-compatible** em `/v1/chat/completions` — compatível com LiteLLM/CrewAI
   diretamente. É o que o sistema usa via `provider: lmstudio`.

2. **Native API v1** em `/api/v1/*` — mais rico (inclui `max_context`, token/s, TTFT),
   mas **não é compatível com LiteLLM** sem integração customizada.

O sistema usa o endpoint OpenAI-compatible para inferência, mas faz uma chamada
HTTP simples ao endpoint nativo para obter a janela de contexto do modelo carregado.
Isso é implementado em `utils/context_checker.py`.

### Configuração LMStudio no config.yaml

```yaml
provider: lmstudio
model: openai/llama-3.2-3b-instruct  # "openai/" prefix obrigatório
base_url: http://localhost:1234/v1
# api_key não precisa ser definido — LMStudio ignora
```

**Importante:** o campo `model` deve corresponder exatamente ao ID do modelo
retornado por `GET http://localhost:1234/v1/models`. Verificar antes de rodar.

---

## 5. Prompts externalizados em YAML

Os prompts de todos os agentes (backstory, goal, task description, expected_output)
são externalizados para `prompts/agents.yaml` e `prompts/tasks.yaml`. O código
Python apenas carrega esses arquivos — nenhum prompt fica hardcoded em Python.

Isso permite que membros da equipe ajustem prompts sem tocar em código.

### Carregamento em Python (padrão para todos os agentes)

```python
import yaml
from pathlib import Path

def load_prompts() -> dict:
    base = Path(__file__).parent.parent / "prompts"
    agents = yaml.safe_load((base / "agents.yaml").read_text(encoding="utf-8"))
    tasks  = yaml.safe_load((base / "tasks.yaml").read_text(encoding="utf-8"))
    return {"agents": agents, "tasks": tasks}
```

---

## 6. Novos prompts completos dos agentes

### prompts/agents.yaml — conteúdo completo

```yaml
reader:
  role: "Leitor de Papers Científicos"
  goal: >
    Extrair o texto completo de um paper científico em PDF e retorná-lo
    limpo e estruturado para análise posterior.
  backstory: >
    Você é especialista em processar documentos acadêmicos em PDF.
    Seu único trabalho é ler o arquivo e retornar o texto completo,
    sem modificar, resumir ou interpretar o conteúdo.
    Não analise — apenas extraia.

summarizer:
  role: "Sumarizador de Papers Científicos"
  goal: >
    Comprimir o texto completo de um paper científico em um sumário denso
    que preserve todos os termos técnicos, resultados quantitativos e a
    estrutura do método proposto, sem omitir informações relevantes.
  backstory: >
    Você é especialista em sintetizar papers científicos de forma estruturada.
    Você recebe o texto completo de um paper (ou segmentos se for muito grande)
    e produz um sumário denso preservando TODA informação necessária para:
    1. Classificar o tipo de contribuição do paper
    2. Identificar conceitos centrais e termos técnicos específicos
    3. Entender o método ou sistema proposto com detalhes técnicos
    4. Conhecer os resultados mensuráveis, métricas e avaliações
    5. Identificar limitações e trabalhos futuros

    REGRAS:
    - Se receber segmentos marcados como [SEGMENTO N], processe cada um
      e consolide em um único sumário coerente.
    - Preserve TODOS os termos técnicos, nomes de algoritmos, ferramentas,
      dispositivos e métricas numéricas encontrados no texto.
    - Não omita resultados quantitativos — percentuais, métricas de desempenho
      e dados de avaliação são essenciais para a classificação posterior.
    - Se o paper implementa um sistema ou jogo, descreva sua estrutura com precisão.

    Formato de saída obrigatório em blocos:
    - CONTRIBUIÇÃO CENTRAL: (1-2 frases diretas)
    - PROBLEMA E CONTEXTO: (2-3 frases)
    - MÉTODO/SISTEMA: (4-6 frases com termos técnicos específicos)
    - RESULTADOS E MÉTRICAS: (3-4 frases com dados quantitativos quando disponíveis)
    - LIMITAÇÕES: (1-2 frases)
    - CONCEITOS-CHAVE: (lista de 6-10 termos técnicos centrais)

classifier:
  role: "Classificador de Papers para Experiências Interativas"
  goal: >
    Analisar o sumário de um paper científico e extrair um perfil estruturado
    que descreve sua contribuição central, potencial de interação e template
    de experiência interativa mais adequado.
  backstory: >
    Você é especialista em analisar papers científicos para geração de
    experiências interativas educacionais em Unity.

    Você conhece os 5 templates disponíveis:
    - T1 Explorer: frameworks, surveys, contribuições conceituais
      → exploração livre com coleta de conhecimento
    - T2 Operator: pipelines, métodos sequenciais, arquiteturas
      → execução sequencial de etapas com feedback
    - T3 Comparator: avaliações, benchmarks, comparações A vs B
      → duas áreas espelhadas + decisão central
    - T4 Tuner: simulações, parâmetros ajustáveis, modelos adaptativos
      → painel de controle com feedback reativo
    - T5 Observer: datasets, resultados quantitativos, experimentos
      → estações de dados + pergunta de síntese

    Se o paper implementa um jogo (implementsGame=true), escolha o template
    que mais se aproxima da ESTRUTURA DO JOGO descrito, não do conteúdo teórico.

    Se nenhum template mapeia bem, use T1 e documente em templateRationale.

designer:
  role: "Designer de Experiências Interativas Educacionais"
  goal: >
    Receber o perfil de um paper científico e produzir um plano de design
    para uma experiência interativa em Unity que comunique a contribuição
    central do paper de forma intuitiva e envolvente.
  backstory: >
    Você é um designer de experiências interativas educacionais.
    Seu trabalho é criar experiências que ajudem pessoas a entender
    contribuições científicas de forma mais intuitiva do que lendo o paper.

    A máxima do seu trabalho é:
    "Qual é a contribuição mais importante desse paper? Como apresentar ela
    de forma interativa para que o usuário, ao navegar pela experiência,
    entenda de modo mais intuitivo e agradável do que apenas lendo o paper?"

    IMPORTANTE: Você está projetando uma EXPERIÊNCIA INTERATIVA, não um jogo.
    A experiência pode incluir elementos de jogo quando o paper trata de jogos,
    mas o foco é sempre comunicar conhecimento — não entreter.
    Pense em museus virtuais, instalações interativas, tours educacionais.

    BUILDING BLOCKS DISPONÍVEIS:

    Informativos: KnowledgeCrystal, InfoSign, EchoStatue, NarratorCaption
    Progressão: Checkpoint, Door, KeyItem, TriggerZone, SpawnPoint
    Interação física: PickupBox, PressurePlate, Lever
    UI e Mídia: DialogueBox, MenuPanel, InventoryBar, MediaWindow
      MediaWindow modos: image | lottie | chart

    REGRA SOBRE MediaWindow:
    MediaWindow NÃO deve ser o único objeto que representa a contribuição
    central do paper. Ela é para resultados, gráficos e imagens de apoio.
    A contribuição central deve ter pelo menos um objeto de interação que
    a represente de forma ativa — KnowledgeCrystal, Lever, ou similar.
    Para conceitos técnicos abstratos (algoritmos, pipelines), combine:
    - Um objeto de interação que representa o processo
    - Uma MediaWindow modo lottie com animação abstrata do conceito

    MAPEAMENTO CONTEÚDO → OBJETO:
    - Problema/obstáculo do paper → Door, bloqueio narrativo
    - Método/solução → KeyItem, Lever, componente de estação (T2)
    - Conceito central → KnowledgeCrystal, EchoStatue
    - Resultado/contribuição → Checkpoint final, MediaWindow
    - Dado/métrica → MediaWindow modo chart
    - Limitação → InfoSign, NarratorCaption de aviso

    REGRAS DE DESIGN:
    - Duração alvo: 3-5 minutos
    - Máximo 3 etapas da experiência
    - Máximo 4 NPCs com diálogo
    - Máximo 2 MediaWindows por experiência
    - Máximo 3 áreas, máximo 8 objetos interativos por área
    - T3: áreas A e B devem ter número idêntico de objetos interativos
    - T2: sequência de áreas é linear, sem branches

    Se nenhum template mapeia bem: use T1 e registre em designNotes.
    Use designNotes para sinalizar decisões ambíguas que a pessoa responsável
    deve revisar antes de implementar.
```

### prompts/tasks.yaml — conteúdo completo

```yaml
read:
  description: >
    Use a ferramenta pdf_reader para ler o arquivo em '{pdf_path}'.
    Retorne o texto extraído exatamente como a ferramenta produziu,
    sem modificar ou resumir o conteúdo.
  expected_output: >
    Texto completo do paper científico extraído do PDF, incluindo
    todas as seções: abstract, introdução, metodologia, resultados,
    avaliação, conclusão e referências.

summarize:
  description: >
    Receba o texto completo do paper extraído pelo agente anterior.
    Produza um sumário denso de aproximadamente 800 palavras nos blocos
    definidos: CONTRIBUIÇÃO CENTRAL, PROBLEMA E CONTEXTO, MÉTODO/SISTEMA,
    RESULTADOS E MÉTRICAS, LIMITAÇÕES, CONCEITOS-CHAVE.
    Preserve todos os termos técnicos específicos e dados quantitativos.
  expected_output: >
    Sumário estruturado em 6 blocos com aproximadamente 800 palavras,
    preservando termos técnicos, algoritmos, ferramentas e métricas do paper.

classify:
  description: >
    Com base no sumário do paper fornecido pelo agente anterior,
    extraia um perfil estruturado seguindo exatamente o schema PaperProfile.
    Retorne SOMENTE JSON válido, sem texto adicional, sem markdown.
  expected_output: >
    JSON válido com todos os campos do PaperProfile:
    paperTitle, domain, centralContribution, hasImplementation,
    hasMeasurableMetrics, isAboutGames, implementsGame, gameStructure,
    interactionPotential, keyConcepts (máx 5),
    suggestedTemplate, templateRationale.

design:
  description: >
    Com base no PaperProfile fornecido pelo agente anterior, produza um
    plano de design para uma experiência interativa em Unity que comunique
    a contribuição central do paper de forma intuitiva.
    Use os building blocks disponíveis e respeite todas as regras de design.
    Retorne SOMENTE JSON válido, sem texto adicional, sem markdown.
  expected_output: >
    JSON válido com todos os campos do DesignPlan:
    templateId, templateRationale, perspective, perspectiveRationale,
    narrativeFrame (openingContext, centralChallenge, resolution),
    objectives (1-3 etapas), areaOutline (1-3 áreas com keyObjects),
    designNotes.
```

---

## 7. Detecção de janela de contexto e chunking adaptativo

### utils/context_checker.py — conteúdo completo

```python
"""
utils/context_checker.py
Detecta janela de contexto do modelo configurado e estima tokens do paper.
Suporta: modelos conhecidos via litellm, LMStudio via API nativa, modelos desconhecidos.
"""
import re
import requests
import litellm
from typing import Optional


# Tamanho máximo seguro sem aviso (70% da janela de contexto)
CONTEXT_SAFETY_FACTOR = 0.70

# Estimativa conservadora: 1 token ≈ 4 caracteres
CHARS_PER_TOKEN = 4

# Janela padrão quando o modelo é desconhecido
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
    # Tenta API nativa do LMStudio primeiro (mais preciso para modelos locais)
    if base_url and "localhost" in base_url:
        lmstudio_context = _get_lmstudio_context(base_url)
        if lmstudio_context:
            return lmstudio_context

    # Tenta litellm para modelos conhecidos
    try:
        info = litellm.get_model_info(model)
        # litellm usa max_input_tokens ou max_tokens dependendo da versão
        return (
            info.get("max_input_tokens")
            or info.get("max_tokens")
        )
    except Exception:
        return None


def _get_lmstudio_context(base_url: str) -> Optional[int]:
    """
    Chama o endpoint nativo do LMStudio (/api/v1/models) para obter max_context.
    O LMStudio 0.4.0+ expõe essa informação na API nativa.
    """
    try:
        # Deriva o base da URL (remove /v1 se presente)
        base = re.sub(r"/v1/?$", "", base_url.rstrip("/"))
        url = f"{base}/api/v1/models"
        resp = requests.get(url, timeout=3)
        if resp.status_code == 200:
            data = resp.json()
            models = data.get("data", [])
            if models:
                # Pega o primeiro modelo carregado
                return models[0].get("max_context_length") or models[0].get("max_context")
    except Exception:
        pass
    return None


def check_and_warn(text: str, model: str, base_url: Optional[str], cfg: dict) -> bool:
    """
    Verifica se o paper cabe na janela de contexto do modelo.
    Retorna True se deve prosseguir, False se o usuário cancelou.

    Exibe aviso e pede confirmação se configurado (confirm_large_papers: true).
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
            print(f"\n  ⚠  O paper excede o limite seguro do modelo.")
            print(f"     Chunking automático será aplicado no Summarizer.")
            if confirm:
                resp = input("  Deseja prosseguir? [S/n] ").strip().lower()
                if resp == "n":
                    print("  Execução cancelada pelo usuário.")
                    return False
        else:
            print(f"  ✓  Paper dentro do limite seguro. Processando normalmente.")
    else:
        print(f"  ⚠  Modelo desconhecido — janela de contexto não detectada.")
        print(f"     Estimativa: ~{token_estimate:,} tokens.")
        if confirm:
            print(f"     Se o modelo tiver janela pequena (ex: < 8k), o processo pode falhar.")
            resp = input("  Deseja prosseguir? [S/n] ").strip().lower()
            if resp == "n":
                print("  Execução cancelada pelo usuário.")
                return False

    return True
```

---

## 8. Interface de seleção de PDF e estimativa de tokens

### utils/pdf_selector.py — conteúdo completo

```python
"""
utils/pdf_selector.py
Interface de seleção de PDF no terminal.
Lista os PDFs disponíveis na pasta papers/ e pede ao usuário que escolha.
Mais robusto do que digitar o caminho completo a cada execução.
"""
from pathlib import Path


PAPERS_DIR = Path(__file__).parent.parent / "papers"


def select_pdf() -> str:
    """
    Lista os PDFs em papers/ e retorna o caminho do PDF escolhido.
    Se a pasta estiver vazia, orienta o usuário a adicionar arquivos.
    """
    if not PAPERS_DIR.exists():
        PAPERS_DIR.mkdir(parents=True)

    pdfs = sorted(PAPERS_DIR.glob("*.pdf"))

    if not pdfs:
        print(f"\n  A pasta papers/ está vazia.")
        print(f"  Adicione arquivos PDF em: {PAPERS_DIR.resolve()}")
        raise SystemExit(1)

    print(f"\n  PDFs disponíveis em papers/:\n")
    for i, pdf in enumerate(pdfs, 1):
        size_kb = pdf.stat().st_size // 1024
        print(f"  [{i}] {pdf.name}  ({size_kb} KB)")

    print()
    while True:
        raw = input("  Selecione o número do PDF [1]: ").strip()
        if raw == "":
            raw = "1"
        try:
            idx = int(raw)
            if 1 <= idx <= len(pdfs):
                selected = pdfs[idx - 1]
                print(f"\n  ✓  Selecionado: {selected.name}")
                return str(selected)
            else:
                print(f"  Número inválido. Digite entre 1 e {len(pdfs)}.")
        except ValueError:
            print(f"  Digite apenas o número.")
```

---

## 9. Sistema de logging completo

### Implementação em crew.py

O logging deve ser configurado no início da execução, antes de qualquer output,
e escrever simultaneamente para o terminal e para um arquivo de log.

```python
import logging
import sys
from pathlib import Path
from datetime import datetime


def setup_logging(paper_id: str) -> logging.Logger:
    """
    Configura logging dual: terminal (INFO) + arquivo (DEBUG com timestamps).
    Arquivo salvo em outputs/{paper_id}/session_{timestamp}.log
    """
    log_dir = Path("outputs") / paper_id
    log_dir.mkdir(parents=True, exist_ok=True)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    log_file = log_dir / f"session_{timestamp}.log"

    logger = logging.getLogger("paper_cave")
    logger.setLevel(logging.DEBUG)

    # Handler para arquivo — tudo, com timestamps
    fh = logging.FileHandler(log_file, encoding="utf-8")
    fh.setLevel(logging.DEBUG)
    fh.setFormatter(logging.Formatter(
        "%(asctime)s [%(levelname)s] %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S"
    ))

    # Handler para terminal — só INFO e acima
    ch = logging.StreamHandler(sys.stdout)
    ch.setLevel(logging.INFO)
    ch.setFormatter(logging.Formatter("%(message)s"))

    logger.addHandler(fh)
    logger.addHandler(ch)

    # Captura também logs de bibliotecas terceiras (crewai, litellm)
    for lib in ["crewai", "litellm"]:
        lib_logger = logging.getLogger(lib)
        lib_logger.addHandler(fh)

    logger.info(f"Log de sessão iniciado: {log_file}")
    return logger
```

---

## 10. Flag --from-step para retomar execuções parciais

Permite retomar uma execução a partir de um passo intermediário, reaproveitando
outputs já salvos. Útil quando um modelo local falha no Designer mas o Classifier
já funcionou — não precisa reprocessar o PDF do zero.

### utils/step_resume.py — conteúdo completo

```python
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
                f"Execute sem --from-step primeiro."
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
```

### Uso no terminal

```bash
# Retoma a partir do Classifier (reusa summary já salvo)
python crew.py --from-step classifier

# Retoma a partir do Designer (reusa summary + profile já salvos)
python crew.py --from-step designer
```

---

## 11. Tratamento de erros de schema (modelos locais)

### Wrapper de execução com retry e interrupção interativa

O CrewAI tem retry automático via `output_pydantic`, mas quando um modelo local
não consegue produzir JSON válido após múltiplas tentativas, o processo falha sem
aviso claro. O wrapper abaixo trata esse caso.

```python
import json
from pydantic import ValidationError


def safe_execute_crew(crew, max_retries: int = 3, logger=None) -> object:
    """
    Executa o crew com tratamento de erros de schema.
    Se o modelo não produzir JSON válido após max_retries tentativas,
    pergunta ao usuário se quer continuar ou abortar.
    """
    for attempt in range(1, max_retries + 1):
        try:
            result = crew.kickoff()
            return result
        except (ValidationError, json.JSONDecodeError, ValueError) as e:
            msg = f"Tentativa {attempt}/{max_retries}: modelo não produziu JSON válido."
            if logger:
                logger.error(f"{msg}\nDetalhe: {e}")
            else:
                print(f"\n  ✗  {msg}")
                print(f"     Detalhe: {e}")

            if attempt == max_retries:
                print(f"\n  O modelo não conseguiu seguir o schema após {max_retries} tentativas.")
                print(f"  Isso é comum em modelos locais menores.")
                print(f"  Sugestões:")
                print(f"    1. Tente um modelo maior ou com mais capacidade de seguir instruções")
                print(f"    2. Verifique os logs em outputs/ para ver o output bruto do modelo")
                print(f"    3. Considere usar um modelo cloud (Gemini, Claude, OpenAI)")
                resp = input("\n  Tentar novamente? [s/N] ").strip().lower()
                if resp == "s":
                    max_retries += 1  # dá mais uma chance
                    continue
                raise
        except Exception as e:
            if logger:
                logger.exception(f"Erro inesperado na execução: {e}")
            raise
```

---

## 12. Atualização dos schemas Pydantic

### models/schemas.py — alterações necessárias

Remover `HealItem` do mapeamento interno de building blocks (documentação).
Renomear campos para linguagem de experiência:

```python
# Em DesignPlan, renomear:
# objectives → experienceSteps  (ou manter 'objectives' mas mudar descrição)
# Manter os campos existentes para não quebrar compatibilidade com outputs já salvos
# Apenas atualizar os Field descriptions

class Objective(BaseModel):
    index: int
    description: str
    mappedConcept: str = Field(
        description="qual conceito do paper esta etapa representa"
    )
    completionTrigger: str = Field(
        description="condição que marca o avanço para a próxima etapa"
    )
```

O schema JSON de saída permanece idêntico para compatibilidade com outputs
já gerados. A mudança é conceitual e está nos prompts.

---

## 13. crew.py refatorado — estrutura completa

O `crew.py` deve ser reescrito para integrar todos os módulos novos.
Abaixo a estrutura completa:

```python
"""
crew.py
Paper Cave Crew — entrada principal.
Uso: python crew.py [--from-step STEP] [--pdf CAMINHO]
"""
import argparse
import json
import sys
from pathlib import Path
from datetime import datetime

from dotenv import load_dotenv
from crewai import Crew, Task, Process

from utils.config_loader import load_config, make_llm, get_config_summary
from utils.pdf_selector import select_pdf
from utils.context_checker import check_and_warn, estimate_tokens
from utils.step_resume import load_intermediate
from crew_agents import (
    make_reader_agent,
    make_summarizer_agent,
    make_classifier_agent,
    make_designer_agent,
)
from models.schemas import PaperProfile, DesignPlan

load_dotenv()


def paper_id_from_path(pdf_path: str) -> str:
    return Path(pdf_path).stem.replace(" ", "_")


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


def setup_logging(paper_id: str):
    """Ver seção 9 — setup_logging completo."""
    import logging, sys
    from datetime import datetime

    log_dir = Path("outputs") / paper_id
    log_dir.mkdir(parents=True, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    log_file = log_dir / f"session_{timestamp}.log"

    logger = logging.getLogger("paper_cave")
    logger.setLevel(logging.DEBUG)

    fh = logging.FileHandler(log_file, encoding="utf-8")
    fh.setLevel(logging.DEBUG)
    fh.setFormatter(logging.Formatter(
        "%(asctime)s [%(levelname)s] %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S"
    ))

    ch = logging.StreamHandler(sys.stdout)
    ch.setLevel(logging.INFO)
    ch.setFormatter(logging.Formatter("%(message)s"))

    logger.addHandler(fh)
    logger.addHandler(ch)

    for lib in ["crewai", "litellm"]:
        logging.getLogger(lib).addHandler(fh)

    logger.info(f"Log: {log_file}")
    return logger


def run(pdf_path: str, from_step: str | None = None):
    cfg       = load_config()
    paper_id  = paper_id_from_path(pdf_path)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")

    logger = setup_logging(paper_id)
    logger.info(f"\n{'='*60}")
    logger.info(f"  Paper Cave — {paper_id}")
    logger.info(f"  Provider: {get_config_summary(cfg)}")
    logger.info(f"  {timestamp}")
    logger.info(f"{'='*60}\n")

    llm = make_llm(cfg)

    # ── Retomada de passo intermediário ────────────────────────────────────
    if from_step:
        logger.info(f"  Retomando a partir de: {from_step}")
        try:
            intermediate = load_intermediate(paper_id, from_step)
        except (FileNotFoundError, ValueError) as e:
            logger.error(str(e))
            sys.exit(1)
    else:
        intermediate = {}

    # ── Leitura do PDF e verificação de contexto ───────────────────────────
    if "summary" not in intermediate:
        # Reader precisa rodar — verificar tamanho do PDF
        import fitz
        doc = fitz.open(pdf_path)
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

    # ── Montagem dos agentes ───────────────────────────────────────────────
    reader     = make_reader_agent(llm)
    summarizer = make_summarizer_agent(llm)
    classifier = make_classifier_agent(llm)
    designer   = make_designer_agent(llm)

    # ── Montagem das tasks (apenas as necessárias) ─────────────────────────
    # [Implementação completa das tasks como nas versões anteriores,
    #  usando os textos de prompts/tasks.yaml carregados via load_prompts()]

    # ── Execução ──────────────────────────────────────────────────────────
    # [crew.kickoff() com safe_execute_crew wrapper]

    # ── Salvamento dos outputs ────────────────────────────────────────────
    logger.info(f"\n  Concluído. Outputs em: outputs/{paper_id}/")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Paper Cave Crew")
    parser.add_argument(
        "--pdf",
        help="Caminho para o PDF. Se omitido, abre seletor interativo.",
        default=None,
    )
    parser.add_argument(
        "--from-step",
        choices=["summarizer", "classifier", "designer"],
        help="Retoma execução a partir deste passo, reaproveitando outputs anteriores.",
        default=None,
    )
    args = parser.parse_args()

    pdf_path = args.pdf or select_pdf()
    run(pdf_path=pdf_path, from_step=args.from_step)
```

**Nota para o Claude Code:** o crew.py acima tem comentários `[Implementação completa...]`
em dois pontos. Nesses pontos, o Claude Code deve implementar a lógica de montagem
de tasks e execução baseando-se na versão atual do crew.py do projeto, adaptando para:
1. Carregar descriptions/expected_output de `prompts/tasks.yaml`
2. Envolver `crew.kickoff()` no wrapper `safe_execute_crew`
3. Passar `from_step` / `intermediate` para pular tasks já completadas

---

## 14. Referências técnicas

### CrewAI — documentação oficial
- Conexão com LLMs e providers: https://docs.crewai.com/en/learn/llm-connections
- Configuração via YAML: https://codesignal.com/learn/courses/getting-started-with-crewai-agents-and-tasks/lessons/configuring-crewai-agents-and-tasks-with-yaml-files

### LiteLLM
- Providers suportados: https://docs.litellm.ai/docs/providers
- `get_model_info()`: https://docs.litellm.ai/docs/completion/token_usage
- Fallbacks por context window: https://docs.litellm.ai/docs/tutorials/model_fallbacks
- model_prices_and_context_window.json (modelos conhecidos):
  https://github.com/BerriAI/litellm/blob/main/model_prices_and_context_window.json

### LMStudio
- Developer docs: https://lmstudio.ai/docs/developer
- OpenAI-compatible endpoint: https://lmstudio.ai/docs/developer/openai-compat
- Native REST API v1 (contexto, stats): https://lmstudio.ai/docs/developer/rest
- Integração com CrewAI: https://m-ruminer.medium.com/using-lm-studio-and-crewai-with-llama-8f8e712e659b

### Formato do model string para LMStudio no CrewAI
```
model="openai/<nome-exato-do-modelo>"
base_url="http://localhost:1234/v1"
api_key="lm-studio"  # qualquer string
```
O nome exato do modelo deve ser obtido via `GET http://localhost:1234/v1/models`.

### .gitignore — adicionar
```
config/config.yaml
.env
outputs/
logs/
__pycache__/
*.pyc
.venv/
```
