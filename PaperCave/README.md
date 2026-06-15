# Paper Cave

Pipeline multi-agente que transforma papers científicos em planos de design
para **experiências interativas educacionais em Unity**.

Dado um PDF acadêmico, o sistema produz automaticamente:
- Um sumário estruturado do paper
- Uma classificação da contribuição e do template de experiência mais adequado
- Um plano de design concreto com áreas, objetos interativos e roteiro narrativo

---

## Como funciona

```
PDF  →  [Reader]  →  [Summarizer]  →  [Classifier]  →  [Designer]  →  DesignPlan
                          ↑                                   ↑
                    Chunking                           RAG Search Tool
                    adaptativo                        (busca no paper
                                                       sob demanda)
```

| # | Agente | Input | Output |
|---|--------|-------|--------|
| 1 | **Reader** | Caminho do PDF | Texto completo extraído |
| 2 | **Summarizer** | Texto do paper | Sumário denso em 6 blocos |
| 3 | **Classifier** | Sumário | `PaperProfile` (JSON tipado) |
| 4 | **Designer** | `PaperProfile` + RAG | `DesignPlan` (JSON tipado) |

Os 4 outputs são salvos em `outputs/{nome_do_pdf}/` para inspeção e reaproveitamento.

---

## Setup

### 1. Pré-requisitos

- Python 3.11+
- Chave de API do provider escolhido (ver tabela abaixo)

### 2. Instalar dependências

```bash
python -m venv .venv

# Linux / macOS
source .venv/bin/activate

# Windows
.venv\Scripts\activate

pip install -r requirements.txt
```

> **Nota:** `sentence-transformers` (~90MB) é baixado na primeira execução para
> prover embeddings locais quando o provider não tem API de embeddings própria.
> Nenhuma configuração adicional necessária.

### 3. Configurar credenciais

```bash
cp .env.example .env
# Edite .env e adicione sua chave de API
```

### 4. Configurar provider e modelo

```bash
cp config/config.example.yaml config/config.yaml
# Edite config/config.yaml com o provider e modelo desejados
```

O `config/config.yaml` é **gitignored** (contém sua configuração pessoal).
O `config/config.example.yaml` é versionado como referência da equipe.

### 5. Adicionar papers

Coloque arquivos PDF na pasta `papers/`.

---

## Uso

```bash
# Seletor interativo — lista os PDFs em papers/ e pergunta qual processar
python crew.py

# Especificando o PDF diretamente
python crew.py --pdf papers/meu_paper.pdf

# Retomada parcial — reusa outputs já gerados
python crew.py --from-step classifier   # reusa sumário, reprocessa do Classifier
python crew.py --from-step designer     # reusa sumário + perfil, reprocessa só o Designer
```

---

## Configuração de Providers

### Google Gemini (recomendado para começar)

```yaml
# config/config.yaml
provider: google
model: gemini-2.5-flash
```

```env
# .env
GEMINI_API_KEY=sua_chave_aqui
```

Obter chave gratuita: https://aistudio.google.com/app/apikey

### OpenAI

```yaml
provider: openai
model: gpt-4o-mini
```

```env
OPENAI_API_KEY=sua_chave_aqui
```

### Anthropic Claude

```yaml
provider: anthropic
model: claude-sonnet-4-5
```

```env
ANTHROPIC_API_KEY=sua_chave_aqui
```

### LMStudio (modelo local)

```yaml
provider: lmstudio
model: openai/nome-exato-do-modelo   # obter via GET http://localhost:1234/v1/models
base_url: http://localhost:1234/v1
```

> **Requisito crítico para modelos locais:** o modelo **deve ter suporte a Tool/Function Calling**
> (necessário para o RAG agêntico do Designer). Modelos recomendados: Llama 3.1 8B+, Qwen 2.5 7B+,
> Mistral Nemo. Modelos sem tool calling desativarão o RAG automaticamente.

### Ollama

```yaml
provider: ollama
model: ollama/llama3.1
base_url: http://localhost:11434
```

---

## RAG Agêntico

O agente Designer é equipado com uma ferramenta de busca semântica (`paper_search`)
que permite consultar o paper original sob demanda.

**Por que isso importa:** o Summarizer produz ~800-1200 palavras do paper, mas papers
densos têm métricas, hardware específico e trechos metodológicos que não cabem no sumário.
O Designer pode buscar esses detalhes em vez de inventá-los.

### Como o sistema decide qual embedding usar

| Provider | Embeddings usados | Modelo |
|----------|-------------------|--------|
| `google` | Gemini Embeddings API | `text-embedding-004` |
| `openai` / `openai_compatible` | OpenAI Embeddings API | `text-embedding-3-small` |
| `anthropic` / `lmstudio` / `ollama` / outros | Local (SentenceTransformers) | `all-MiniLM-L6-v2` |

**Nota sobre Anthropic:** a Anthropic não oferece API de embeddings própria.
O sistema usa SentenceTransformers local automaticamente.

**Nota sobre LMStudio/Ollama:** esses runtimes raramente expõem API de embeddings
no endpoint OpenAI-compat. O fallback local garante que o RAG funcione em qualquer setup.

### Trocando o modelo de embeddings local

Para usar outro modelo SentenceTransformers, edite `utils/rag_indexer.py`:

```python
# Linha: return _local_embed()
return _local_embed("BAAI/bge-small-en-v1.5")  # modelo alternativo mais preciso
```

Modelos leves recomendados para CPU: `all-MiniLM-L6-v2` (padrão, 90MB),
`paraphrase-multilingual-MiniLM-L12-v2` (multilíngue, 420MB).

---

## Suporte a Modelos com Raciocínio (Thinking)

Modelos como DeepSeek R1, Qwen QwQ e similares produzem blocos `<think>...</think>`
antes da resposta final. O sistema detecta e remove essas tags automaticamente antes
de tentar parsear o JSON, evitando erros de schema.

---

## Templates de Experiência

O Classifier escolhe um dos 5 templates com base no tipo de contribuição:

| Template | Tipo de paper | Estrutura da experiência |
|----------|--------------|--------------------------|
| **T1 Explorer** | Frameworks, surveys, contribuições conceituais | Exploração livre com coleta de conhecimento |
| **T2 Operator** | Pipelines, métodos sequenciais, arquiteturas | Execução sequencial de etapas com feedback |
| **T3 Comparator** | Avaliações, benchmarks, comparações A vs B | Duas áreas espelhadas + painel de síntese |
| **T4 Tuner** | Simulações, parâmetros ajustáveis | Sala de controle com feedback reativo |
| **T5 Observer** | Datasets, resultados quantitativos | Ilhas de dados com narrador especialista |

---

## Estrutura de pastas

```
paper_cave/
├── config/
│   ├── config.example.yaml  # template de configuração (versionar)
│   └── config.yaml          # configuração local (gitignored)
├── prompts/
│   ├── agents.yaml          # role, goal, backstory + few-shots de todos os agentes
│   └── tasks.yaml           # description e expected_output de cada task
├── crew_agents/
│   ├── reader.py
│   ├── summarizer.py
│   ├── classifier.py        # max_retry_limit + make_json_llm
│   └── designer.py          # max_retry_limit + RAG search_tool
├── models/
│   └── schemas.py           # Pydantic: PaperProfile (com @model_validator), DesignPlan
├── tools/
│   └── pdf_tool.py          # tool CrewAI para extração de PDF com chunking
├── utils/
│   ├── config_loader.py     # make_llm, make_json_llm, get_embed_fn
│   ├── context_checker.py   # estima tokens, detecta janela de contexto
│   ├── pdf_selector.py      # seletor interativo de PDF no terminal
│   ├── rag_indexer.py       # embeddings por provider, índice numpy, PaperSearchTool
│   ├── step_resume.py       # retomada via --from-step (legado, simplificado no crew.py)
│   └── thinking_stripper.py # remove <think> tags de modelos de raciocínio
├── outputs/                 # gerado automaticamente (gitignored)
├── papers/                  # PDFs de entrada
├── crew.py                  # entrada principal
├── requirements.txt
├── .env.example
└── .gitignore
```

---

## Outputs gerados

```
outputs/{nome_do_pdf}/
├── 01_reader_output.json       # texto completo extraído
├── 02_summarizer_output.json   # sumário em 6 blocos
├── 03_classifier_output.json   # PaperProfile (JSON)
├── 04_designer_output.json     # DesignPlan (JSON)
└── session_YYYYMMDD_HHMMSS.log # log completo da sessão
```

---

## Ajustando prompts

Os prompts estão em `prompts/agents.yaml` e `prompts/tasks.yaml` — editáveis sem tocar em código.
Os exemplos few-shot dos 5 templates estão no backstory do Designer em `agents.yaml`.

---

## Robustez

| Problema | Solução |
|----------|---------|
| Modelo não produz JSON válido | `max_retry_limit=3` por agente; CrewAI injeta o erro Pydantic no prompt |
| Modelo produz `<think>` tags | `thinking_stripper.py` limpa o output antes do parse |
| Paper excede janela de contexto | `pdf_tool.py` divide em chunks; Summarizer consolida |
| Execução falha no Designer | `--from-step designer` retoma sem reprocessar o PDF |
| `gameStructure` preenchido incorretamente | `@model_validator` em `PaperProfile` força `"none"` quando `implementsGame=False` |

---

## Próximas etapas (roadmap)

- **Level Designer**: posicionamento de objetos em grade 2D
- **Assembler**: geração do GDD completo com eventGraph
- **Writer**: diálogos dos EchoStatues ancorados nos keyConcepts do paper
- **QA Reviewer**: validação de fidelidade ao paper e conformidade com regras de design
