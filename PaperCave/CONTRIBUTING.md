# Contribuindo com o Paper Cave

Guia de trabalho para a equipe: onde editar cada coisa, como testar, convenções.

---

## Mapa de responsabilidades

| O que você quer mudar | Onde editar |
|-----------------------|-------------|
| Comportamento de um agente (como ele pensa) | `prompts/agents.yaml` |
| Exemplos few-shot dos templates T1-T5 | `prompts/agents.yaml` — seção `designer.backstory` |
| O que uma task pede / o que espera de volta | `prompts/tasks.yaml` |
| Estrutura dos JSONs de saída (campos, tipos) | `models/schemas.py` |
| Regras de validação automática dos schemas | `models/schemas.py` — `@model_validator` |
| Provider / modelo de LLM | `config/config.yaml` (local, não versionar) |
| Qual modelo de embeddings usar para RAG | `utils/rag_indexer.py` — função `_local_embed()` |
| Lógica de extração do PDF | `tools/pdf_tool.py` |
| Lógica de orquestração do pipeline | `crew.py` |
| Construção de LLMs (response_format, etc.) | `utils/config_loader.py` |

---

## Setup do ambiente local

```bash
python -m venv .venv
.venv\Scripts\activate          # Windows
# source .venv/bin/activate     # Linux/macOS

pip install -r requirements.txt

cp .env.example .env
# Adicione sua chave de API (ex: GEMINI_API_KEY)

cp config/config.example.yaml config/config.yaml
# Configure provider e modelo
```

---

## Rodando o sistema

```bash
# Fluxo completo com seletor interativo
python crew.py

# Especificando o PDF
python crew.py --pdf papers/meu_paper.pdf

# Retomada de passo intermediário
python crew.py --from-step classifier   # reusa sumário salvo em outputs/
python crew.py --from-step designer     # reusa sumário + perfil salvos
```

---

## Como o `--from-step` funciona internamente

O `--from-step` **não pula tasks** dentro do mesmo Crew — isso corromperia o grafo
DAG do CrewAI, que perde as referências de input das tasks não executadas.

Em vez disso, o `crew.py` constrói um `Crew` diferente com apenas as tasks necessárias,
**injetando o estado intermediário diretamente na descrição da task** (sem dependência
de contexto de tasks anteriores):

```
--from-step classifier:
  Carrega outputs/paper_id/02_summarizer_output.json
  → Injeta no campo description da task_classify
  → Cria Crew(tasks=[task_classify, task_design])

--from-step designer:
  Carrega outputs/paper_id/03_classifier_output.json
  → Injeta no campo description da task_design
  → Cria Crew(tasks=[task_design])
```

---

## LLMs diferenciados por agente

O sistema usa dois LLMs diferentes intencionalmente:

| Agente | LLM usado | Por quê |
|--------|-----------|---------|
| Reader, Summarizer | `make_llm()` | Saída em texto livre — sem restrição |
| Classifier | `make_json_llm()` | `response_format=json_object` para eliminar markdown e texto livre; **sem tools** |
| Designer | `make_llm()` | **Tem tools** (RAG); `response_format` conflita com tool-calling |

Se o Classifier ainda produzir markdown mesmo com `make_json_llm()`, o problema está
no modelo (local muito pequeno). Use `--from-step classifier` para retentar.

---

## RAG Agêntico — como funciona

1. O texto do PDF é extraído antes do crew rodar (já acontece para verificação de contexto)
2. `utils/rag_indexer.py` divide o texto em chunks de ~1000 chars com overlap de 200
3. Calcula embeddings de todos os chunks usando o provider configurado
4. Cria um `PaperSearchTool` (CrewAI `BaseTool`) que busca por similaridade cosseno
5. O Designer recebe essa tool como `tools=[search_tool]`

O Designer decide autonomamente quando usar a busca — geralmente quando precisa de
métricas específicas, hardware, ou trechos metodológicos não presentes no sumário.

### Trocar o modelo de embeddings local

Edite `utils/rag_indexer.py`, função `get_embed_fn()`:

```python
# Para usar um modelo multilíngue maior:
return _local_embed("paraphrase-multilingual-MiniLM-L12-v2")

# Para usar um modelo mais preciso (mas mais lento):
return _local_embed("BAAI/bge-small-en-v1.5")
```

---

## Suporte a modelos com raciocínio (thinking)

Modelos como DeepSeek R1, Qwen QwQ, etc. produzem blocos `<think>...</think>`.
O `utils/thinking_stripper.py` remove esses blocos automaticamente do output.

O fluxo em `crew.py`:
1. Após `crew.kickoff()`, para cada task com `output_pydantic`
2. Se `task_out.pydantic is None` (parse falhou): chama `_recover_pydantic()`
3. `_recover_pydantic()` chama `extract_json_from_output()` que remove thinking tags
   e extrai o bloco JSON mais externo
4. Tenta re-validar com Pydantic

---

## Validação automática de schema (`@model_validator`)

O `PaperProfile` em `models/schemas.py` tem um validador automático:

```python
@model_validator(mode="after")
def enforce_game_structure(self) -> "PaperProfile":
    if not self.implementsGame:
        self.gameStructure = "none"
    return self
```

**Por quê:** modelos menores ignoram lógicas condicionais negativas em prompts
("se não for jogo, retorne none"). O validador aplica a regra no Python,
independentemente do que o modelo retornar.

Se precisar adicionar mais regras desse tipo, adicione `@model_validator` em
`models/schemas.py` — não no prompt.

---

## Fluxo para ajustar prompts

1. Edite `prompts/agents.yaml` ou `prompts/tasks.yaml`
2. Rode `python crew.py --pdf papers/seu_paper.pdf`
3. Compare os outputs em `outputs/{paper_id}/` com outputs anteriores
4. Se melhorou, commite apenas os YAMLs modificados

### Para ajustar o Designer especificamente

Os exemplos few-shot dos 5 templates estão no final do campo `backstory` do `designer`
em `prompts/agents.yaml`. Edite os blocos JSON lá para ajustar o comportamento.

---

## Adicionando um novo agente

1. Crie `crew_agents/novo_agente.py` seguindo o padrão dos existentes
2. Adicione a seção no `prompts/agents.yaml`
3. Adicione a task em `prompts/tasks.yaml`
4. Exporte a factory function em `crew_agents/__init__.py`
5. Se o agente produz output estruturado, adicione o schema em `models/schemas.py`
6. Integre no pipeline em `crew.py`

---

## Convenções de commit

```
feat: adiciona agente Level Designer
fix: corrige extração de JSON quando modelo retorna thinking tags
prompt: ajusta few-shot T4 para enfatizar MediaWindow reativa
refactor: separa lógica de retry do kickoff principal
docs: atualiza CONTRIBUTING com seção de RAG
```

---

## O que NÃO commitar

- `config/config.yaml` — provider/modelo pessoal (gitignored)
- `.env` — chaves de API (gitignored)
- `outputs/` — texto de papers com direitos autorais (gitignored)
- PDFs em `papers/` — idem

---

## Dúvidas frequentes

**O Classifier falha com erro de JSON.**
1. Use `--from-step classifier` para retentar sem reprocessar o PDF
2. Aumente `max_schema_retries` em `config.yaml`
3. Se for modelo local: verifique se ele tem suporte real a JSON/structured output

**O Designer não usa a ferramenta de busca.**
É comportamento normal quando o sumário contém informação suficiente.
Se quiser forçar o uso, adicione uma instrução no backstory do designer em `agents.yaml`.

**RAG está sendo desativado na inicialização.**
Verifique o log `outputs/paper_id/session_*.log` — a linha "RAG agêntico desativado"
mostra o motivo. Causas comuns: pacote `sentence-transformers` não instalado,
ou erro de autenticação na API de embeddings.

**Modelo local não respeita o schema JSON.**
Modelos com menos de ~7B de parâmetros ou sem fine-tuning de instruction-following
costumam ter esse problema. Considere um modelo cloud ou um local maior.
