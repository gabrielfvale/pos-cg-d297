# PaperCave — Plano de Implementação (Steps 1–4)

Documento para implementação via Claude Code.  
Cada step é independente. Executar na ordem listada.

---

## Contexto da arquitetura atual

```
crew.py pipeline (Python)
  └─ 07_reviewer_output.json   ← ReviewedUnitManifest (suporta stacks)
       └─ unity_export.py      ← FLATTEN stacks → manifest.json
            └─ Assets/PaperCaveData/{paper_id}/manifest.json
                 └─ PaperCaveManifestLoader.cs (Editor)
                      └─ PaperCaveSceneBuilder3D.cs
                           └─ GameObjects na cena
```

**Problema central do Step 2:** `unity_export.py` destrói stacks antes de exportar.  
**Problema central do Step 1:** `BuildExpandedChart/Table` ignoram `content.data` e mostram só descrição textual.

---

## Step 1 — Chart e Table rendering a partir de `content.data`

### Contexto

`PaperCardContent.data` é `object` em C#; Newtonsoft.Json desserializa como `Newtonsoft.Json.Linq.JObject`.  
`BuildCard03` (hardcoded) já demonstra o padrão completo de renderização de barras.  
`BuildCard04` (hardcoded) já demonstra renderização de tabela com TMPro rich text.  
O objetivo é replicar esses padrões em `BuildExpandedChart` e `BuildExpandedTable` usando dados dinâmicos do JSON.

### 1.1 — Adicionar `using Newtonsoft.Json.Linq;`

**Arquivo:** `Assets/Editor/PaperCaveSceneBuilder3D.cs`  
Adicionar no bloco de `using` no topo, junto aos outros.

---

### 1.2 — Reescrever `BuildExpandedChart`

**Arquivo:** `Assets/Editor/PaperCaveSceneBuilder3D.cs`  
**Método:** `static void BuildExpandedChart(CardParts c, PaperUnitData data, Color catColor)`

Lógica a implementar:

1. Tentar parsear `data.content?.data` como `JObject` (`jobj = data.content.data as JObject ?? (data.content.data != null ? JObject.FromObject(data.content.data) : null)`).

2. Se `jobj` não é null e contém `"labels"` e `"values"`:
   - Extrair `labels` como `string[]` de `jobj["labels"]`
   - Extrair `values` como `float[]` de `jobj["values"]`
   - Extrair `average` como `float?` de `jobj["average"]` (opcional)
   - Extrair `unit` como `string` de `jobj["unit"]` (opcional, para legenda do eixo Y)
   - Renderizar bar chart **seguindo exatamente o padrão de `BuildCard03`** (linhas 577–660 do arquivo atual):
     - `PlotFrame` container com `Band(_, 62f, 92f, 8f, 8f)`
     - `plotH = 56f`, `baseY = 16f`, `leftPad = 0.04f`, `rightPad = 0.02f`
     - Calcular `yMin = 0f`, `yMax` = arredondar para cima o max dos valores (ex: `Mathf.Ceil(values.Max() * 1.2f)`)
     - Para cada barra: `AddImage` + rótulo de valor acima + rótulo de eixo abaixo
     - Se `average != null`: linha de referência horizontal + label "avg X.XX" (padrão de BuildCard03)
   - Adicionar nota ao final com `data.content.description` (máx 3 linhas, `Band(_, 156f, 24f, 10f, 10f)`)

3. Se `jobj` é null ou malformado: fallback para texto — renderizar só `data.content.description` (comportamento atual).

4. Para `chartType == "grouped_bar"`: renderizar como bar chart simples por ora (o grouped_bar é future work).

---

### 1.3 — Reescrever `BuildExpandedTable`

**Arquivo:** `Assets/Editor/PaperCaveSceneBuilder3D.cs`  
**Método:** `static void BuildExpandedTable(CardParts c, PaperUnitData data, Color catColor)`

Lógica a implementar:

1. Tentar parsear `data.content?.data` como `JObject`.

2. Se `jobj` contém `"columns"` e `"rows"`:
   - Extrair `columns` como `string[]`
   - Extrair `rows` como `string[][]` (cada row é um array de strings)
   - Renderizar **seguindo exatamente o padrão de `BuildCard04`** (linhas 668–705):
     - `StringBuilder` com TMPro rich text
     - Header de colunas em bold com cor da categoria (`<b><color={hex}>{col0}<pos=58%>{col1}</color></b>`)
     - Cada linha: `{row[0]}<pos=58%>{row[1]}` (Truncate a 20 chars por célula)
     - Para tabelas com 3 colunas: usar `<pos=40%>` e `<pos=75%>`
     - `Band(table.rectTransform, 60f, 74f, 10f, 10f)`
   - Adicionar nota ao final com `data.content.description` (máx 3 linhas)

3. Fallback textual se dados ausentes (comportamento atual).

---

## Step 2 — Stacks de cartas

### Contexto

O schema Python `Unit` com `type="stack"` define 2–4 items, cada um com `title`, `contentType`, e `content` próprios.  
O `AnimationFrameView3D` já implementa flip entre frames com prev/next buttons — a lógica de stack reutiliza essa mecânica, mas com conteúdo heterogêneo por item (cada item tem seu próprio contentType, não só texto).

### 2.1 — Python: remover flatten em `unity_export.py`

**Arquivo:** `PaperCave/utils/unity_export.py`

Mudanças:
- Remover `from utils.flatten_for_compat import flatten_stacks_for_compat`
- Substituir `flat_manifest = flatten_stacks_for_compat(manifest_dict)` por `flat_manifest = manifest_dict` (renomear a variável ou usar diretamente)
- Atualizar o docstring do arquivo removendo a menção ao flatten

O arquivo `flatten_for_compat.py` pode ser mantido como utilitário standalone, mas não deve mais ser chamado automaticamente.

**Atenção:** `_copy_figures` atualmente só busca `assetReference` em `unit["content"]`. Com stacks, as referências ficam em `unit["items"][i]["content"]["assetReference"]`. Atualizar `_copy_figures` para também iterar sobre `unit.get("items") or []` e coletar refs de `item["content"].get("assetReference")`.

---

### 2.2 — C#: adicionar StackItemData ao data model

**Arquivo:** `Assets/Editor/PaperCaveManifestLoader.cs`

Adicionar ao final do arquivo (antes do último `}`):

```csharp
[Serializable]
public class StackItemData
{
    public int?   index;
    public string title;
    public string contentType;
    public PaperCardContent content;
}
```

Adicionar em `PaperUnitData`:
```csharp
public string          stackLabel;
public List<StackItemData> items;
```

---

### 2.3 — C#: novo script `StackFlipController.cs`

**Arquivo:** `Assets/Scripts/PaperCave/StackFlipController.cs` (arquivo novo)

Responsabilidade: controlar qual painel de conteúdo está visível dentro de uma carta stack.

```csharp
namespace PaperCave
{
    public class StackFlipController : MonoBehaviour
    {
        public GameObject[]        panels;       // um panel por stack item (pré-construídos pelo builder)
        public TextMeshProUGUI     counterText;  // "1 / N"
        public TextMeshProUGUI     itemTitle;    // título do item atual (header dentro do expanded)
        public AnimationFrameView3D.Transition transition = AnimationFrameView3D.Transition.Fade;

        int _current = 0;

        void Start() => Apply();

        public void Next() { _current = (_current + 1) % panels.Length; Apply(); }
        public void Prev() { _current = (_current - 1 + panels.Length) % panels.Length; Apply(); }

        void Apply()
        {
            for (int i = 0; i < panels.Length; i++)
                panels[i].SetActive(i == _current);
            if (counterText) counterText.text = $"{_current + 1} / {panels.Length}";
        }
    }
}
```

**Nota:** adicionar `using TMPro;` e `using UnityEngine;` nos imports.

---

### 2.4 — C#: builder suporte a stacks

**Arquivo:** `Assets/Editor/PaperCaveSceneBuilder3D.cs`

#### 2.4a — Em `BuildCardFromData`: detectar type="stack" antes do switch de contentType

```csharp
// Antes do switch(data.contentType):
if (data.type == "stack")
{
    BuildStackCard(c, data, paperId, catColor);
    return;  // pula o switch de contentType
}
```

A lógica de collapsed face para stacks é diferente: usar `data.stackLabel` em vez de `data.title`. Portanto, o `CreateCard` precisa ser chamado com `data.stackLabel ?? data.id` como título e um summary indicando que é um grupo (ex: `$"▶ {data.items?.Count ?? 0} items"`).

Ajustar a chamada de `CreateCard` no início de `BuildCardFromData`:

```csharp
string displayTitle   = (data.type == "stack") ? (data.stackLabel ?? data.id) : (data.title ?? "");
string displaySummary = (data.type == "stack")
    ? $"▶ {data.items?.Count ?? 0} cards — {data.category}"
    : summary;

var c = CreateCard(data.id ?? "unit", w, h, pos, yRot,
    category, catColor, displayTitle, displaySummary);
```

#### 2.4b — Novo método `BuildStackCard`

```csharp
static void BuildStackCard(CardParts c, PaperUnitData data, string paperId, Color catColor)
{
    var e = c.expanded;
    BuildExpandedHeader(e, data.category, catColor, data.stackLabel ?? data.id);

    var items = data.items;
    if (items == null || items.Count == 0)
    {
        // fallback: mostrar stackLabel + category como text_panel
        BuildExpandedTextPanel(c, new PaperUnitData {
            content = new PaperCardContent { description = data.category }
        }, catColor);
        return;
    }

    // Construir um painel de conteúdo por item
    var panels = new GameObject[items.Count];
    for (int i = 0; i < items.Count; i++)
    {
        var item = items[i];
        var panelGO = NewUI($"StackItem_{i}", e).gameObject; // container
        // reutilizar BuildExpanded* adaptado para StackItemData:
        BuildStackItemContent(panelGO.transform, item, paperId, catColor, c.pxW, c.pxH);
        panelGO.SetActive(i == 0);
        panels[i] = panelGO;
    }

    // Counter
    var counter = AddTMP(e, "Counter", $"1 / {items.Count}", 7f, Alpha(White, 0.6f),
        false, false, TextAlignmentOptions.Center, false);
    Band(counter.rectTransform, 134f, 14f, 6f, 6f);

    // StackFlipController
    var flip = c.expanded.gameObject.AddComponent<StackFlipController>();
    flip.panels = panels;
    flip.counterText = counter;

    // Botões Prev/Next (reusa MakeAnimButton adaptado para StackFlipController)
    var buttons = new GameObject("FlipButtons");
    buttons.transform.SetParent(c.root.transform, false);
    buttons.SetActive(false);
    c.card.expandedExtra = buttons;

    MakeFlipButton(buttons.transform, "PrevButton", "‹ PREV", catColor, flip, -1,
        new Vector3(-0.33f, -0.74f, -0.03f));
    MakeFlipButton(buttons.transform, "NextButton", "NEXT ›", catColor, flip, +1,
        new Vector3( 0.33f, -0.74f, -0.03f));
}
```

#### 2.4c — Método auxiliar `BuildStackItemContent`

Similar aos `BuildExpanded*` existentes, mas recebe `StackItemData` em vez de `PaperUnitData`. Converte o item para um `PaperUnitData` temporário para reutilizar os builders existentes:

```csharp
static void BuildStackItemContent(Transform parent, StackItemData item,
    string paperId, Color catColor, float pxW, float pxH)
{
    // Criar um PaperUnitData temporário com os dados do item
    var unitProxy = new PaperUnitData
    {
        id          = item.index?.ToString() ?? "0",
        type        = "card",
        title       = item.title,
        contentType = item.contentType,
        content     = item.content,
        category    = "",
    };

    // Título do item dentro do painel
    if (!string.IsNullOrEmpty(item.title))
    {
        var titleTMP = AddTMP(parent, "ItemTitle", item.title, 10f, White, true, false,
            TextAlignmentOptions.TopLeft, true);
        Band(titleTMP.rectTransform, 42f, 18f, 8f, 8f);
    }

    // Criar um CardParts temporário apontando para o parent
    // (os BuildExpanded* usam c.expanded como parent dos filhos)
    var tempCard = new CardParts { expanded = parent, pxW = pxW, pxH = pxH, catColor = catColor };

    switch (item.contentType)
    {
        case "figure":     BuildExpandedFigure(tempCard, unitProxy, paperId, catColor); break;
        case "chart":      BuildExpandedChart(tempCard, unitProxy, catColor); break;
        case "table":      BuildExpandedTable(tempCard, unitProxy, catColor); break;
        case "animation":  BuildExpandedAnimation(tempCard, unitProxy, catColor); break;
        default:           BuildExpandedTextPanel(tempCard, unitProxy, catColor); break;
    }
}
```

#### 2.4d — `MakeFlipButton` (análogo a `MakeAnimButton`)

Igual a `MakeAnimButton` mas o botão chama `flip.Next()` ou `flip.Prev()` em vez de `view.Advance()`.  
Criar um `Card3DButton`-equivalente para `StackFlipController` **ou** reutilizar `Card3DButton` adicionando suporte a `StackFlipController` como target alternativo.

**Recomendação:** Criar um `StackFlipButton.cs` simples:

```csharp
namespace PaperCave
{
    [RequireComponent(typeof(Collider))]
    public class StackFlipButton : MonoBehaviour
    {
        public StackFlipController target;
        public int direction = 1; // +1 = next, -1 = prev

        void OnMouseDown()
        {
            if (direction > 0) target?.Next();
            else               target?.Prev();
        }
    }
}
```

`MakeFlipButton` é idêntico a `MakeAnimButton` mas adiciona `StackFlipButton` em vez de `Card3DButton`.

---

## Step 3 — Limpeza de campos desnecessários no export

### 3.1 — Stripping em `unity_export.py`

**Arquivo:** `PaperCave/utils/unity_export.py`

Adicionar função `_strip_unity_irrelevant(manifest: dict) -> dict` e chamá-la antes de escrever o manifest.json:

```python
def _strip_unity_irrelevant(manifest: dict) -> dict:
    """Remove campos que só servem para o pipeline Python e não têm função no Unity."""
    import copy
    m = copy.deepcopy(manifest)

    # Campos raiz: metadados do Reviewer, não renderizados
    for key in ("objectScores", "totalAttempts", "assembledFromMultipleAttempts",
                "implementationNotes", "flattenedFromStacks"):
        m.pop(key, None)

    for unit in m.get("units", []):
        # Campos de grounding do Reviewer — não renderizados em Unity
        for key in ("conceptualOrigin", "whyThisUnit"):
            unit.pop(key, None)
        # colorName é redundante com categoryColor (hex já está lá)
        if isinstance(unit.get("styleHint"), dict):
            unit["styleHint"].pop("colorName", None)
        # Mesmo para items dentro de stacks
        for item in unit.get("items") or []:
            for key in ("conceptualOrigin", "whyThisUnit"):
                item.pop(key, None)

    return m
```

Na função `export_to_unity`, após obter `flat_manifest`:
```python
flat_manifest = _strip_unity_irrelevant(flat_manifest)
```

### 3.2 — Verificação do modo padrão no `crew.py`

`simple_mode=False` já é o padrão — **nenhuma mudança necessária**. Confirmar que chamadas de `run_paper()` externas (ex: `run_all_papers.py`) também não passam `simple_mode=True` por padrão.

---

## Step 4 — VFX Library (ScriptableObject) com wiring automático no builder

### Contexto

`VFXManager.cs` já existe com `[SerializeField] private List<VFXEntry> effects`.  
`CardClickVFXBridge.cs` já existe e funciona (detecta toggle `Card3D.Expanded` e chama `VFXManager.PlayEffect(effectIndex)`).  
O problema: o builder de manifesto não adiciona esses componentes nas cartas construídas.

---

### 4.1 — Tornar `effects` acessível programaticamente

**Arquivo:** `Assets/Scripts/VFXManager.cs`

Adicionar método público:
```csharp
public void AddEffect(VFXEntry entry) => effects.Add(entry);
public void ClearEffects() => effects.Clear();
```

Ou simplesmente trocar `[SerializeField] private` por `[SerializeField] public` — ambas funcionam para acesso editor-time via SceneBuilder.

---

### 4.2 — Criar `PaperCaveVFXLibrary.cs`

**Arquivo:** `Assets/Scripts/PaperCave/PaperCaveVFXLibrary.cs` (arquivo novo)

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace PaperCave
{
    /// <summary>
    /// ScriptableObject que define os presets de VFX disponíveis para cartas.
    /// Adicione presets aqui conforme novos prefabs VFX forem encontrados.
    /// O preset no índice 0 é o default aplicado a todas as cartas pelo builder.
    /// </summary>
    [CreateAssetMenu(menuName = "PaperCave/VFX Library",
                     fileName = "PaperCaveVFXLibrary")]
    public class PaperCaveVFXLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Preset
        {
            [Tooltip("Nome legível para identificar o preset no Inspector.")]
            public string presetName = "Lightning Aura";

            [Tooltip("Prefab VFX a instanciar quando a carta for expandida.")]
            public GameObject prefab;

            [Tooltip("Offset local relativo ao centro da carta.")]
            public Vector3 localOffset = Vector3.zero;

            [Tooltip("Multiplicador de escala aplicado ao prefab instanciado.")]
            public Vector3 scaleMultiplier = Vector3.one;

            [Tooltip("Segundos até destruir a instância (0 = não destruir).")]
            public float destroyAfter = 5f;

            [Tooltip("Se true, o VFX segue a carta quando arrastada.")]
            public bool followTarget = true;
        }

        [Tooltip("Lista de presets VFX. Índice 0 = default para todas as cartas.")]
        public List<Preset> presets = new();
    }
}
```

---

### 4.3 — Criar o asset da library

Após criar o script acima, no Editor Unity:
- Right-click em `Assets/PaperCave3D/` → Create → PaperCave → VFX Library
- Salvar como `PaperCaveVFXLibrary.asset`
- Adicionar um preset na lista:
  - `presetName`: "Lightning Aura"
  - `prefab`: arrastar o prefab Lightning Aura da cena `InstantiateVFX` (encontrado em `Assets/VFX/Magic effects pack/Prefabs/` ou similar — confirmar localização no Project)
  - `destroyAfter`: 5
  - `followTarget`: true

**Esse é o único preset default.** Novos presets são adicionados manualmente à lista conforme o dev encontrar prefabs úteis.

---

### 4.4 — Adicionar constante de path e wiring no `PaperCaveSceneBuilder3D`

**Arquivo:** `Assets/Editor/PaperCaveSceneBuilder3D.cs`

Adicionar constante:
```csharp
const string VFXLibraryPath = "Assets/PaperCave3D/PaperCaveVFXLibrary.asset";
```

Adicionar método `WireVFX`:
```csharp
static void WireVFX(CardParts c, PaperCaveVFXLibrary library)
{
    if (library == null || library.presets.Count == 0) return;

    // Um VFXManager por carta, com todos os presets da library
    var mgr = c.root.AddComponent<VFXManager>();

    foreach (var preset in library.presets)
    {
        mgr.AddEffect(new VFXManager.VFXEntry
        {
            prefab          = preset.prefab,
            spawnPoint      = c.root.transform,
            localOffset     = preset.localOffset,
            scaleMultiplier = preset.scaleMultiplier,
            destroyAfter    = preset.destroyAfter,
            followTarget    = preset.followTarget,
        });
    }

    // Bridge: detecta toggle do Card3D e dispara PlayEffect(0) por default
    var bridge = c.root.AddComponent<CardClickVFXBridge>();
    bridge.vfxManager  = mgr;
    bridge.effectIndex = 0;  // Lightning Aura por default; dev muda no Inspector
}
```

#### 4.4a — Chamar `WireVFX` em `BuildCardFromData`

No final de `BuildCardFromData` (após o switch de contentType e o highlight primário):

```csharp
var vfxLibrary = AssetDatabase.LoadAssetAtPath<PaperCaveVFXLibrary>(VFXLibraryPath);
WireVFX(c, vfxLibrary);
```

`AssetDatabase.LoadAssetAtPath` é cached pelo Unity; a chamada por carta tem custo desprezível.

#### 4.4b — Tornar `VFXEntry` public

**Arquivo:** `Assets/Scripts/VFXManager.cs`

`VFXEntry` precisa ser acessível fora do `VFXManager` para o SceneBuilder construir instâncias dela.  
Mover a classe `VFXEntry` para fora do `VFXManager` ou torná-la pública:
```csharp
// Trocar:
[System.Serializable]
public class VFXEntry { ... }

// Já é pública por estar como classe aninhada pública — verificar se o SceneBuilder
// consegue referenciá-la como VFXManager.VFXEntry. Se não, extrair para classe top-level.
```

---

## Resumo dos arquivos modificados

| Arquivo | Tipo de mudança |
|---|---|
| `Assets/Editor/PaperCaveSceneBuilder3D.cs` | BuildExpandedChart, BuildExpandedTable, BuildCardFromData, BuildStackCard (novo), WireVFX (novo) |
| `Assets/Editor/PaperCaveManifestLoader.cs` | `StackItemData` class, campos `stackLabel`/`items` em `PaperUnitData` |
| `Assets/Scripts/VFXManager.cs` | `AddEffect()`/`ClearEffects()` methods, `VFXEntry` acessibilidade |
| `Assets/Scripts/PaperCave/StackFlipController.cs` | Arquivo novo |
| `Assets/Scripts/PaperCave/StackFlipButton.cs` | Arquivo novo |
| `Assets/Scripts/PaperCave/PaperCaveVFXLibrary.cs` | Arquivo novo (ScriptableObject) |
| `PaperCave/utils/unity_export.py` | Remover flatten, adicionar `_strip_unity_irrelevant` |

---

## Ordem de implementação sugerida para Claude Code

1. **Step 3** primeiro (limpeza Python) — mais simples, nenhuma dependência
2. **Step 1** (chart/table rendering) — sem novas dependências, maior impacto visual imediato
3. **Step 2** (stacks) — depende do data model expandido do Step 1/3
4. **Step 4** (VFX library) — independente, pode ser feito a qualquer momento

---

## Verificação funcional esperada ao final

- Carta com `contentType="chart"` e `content.data` preenchido → exibe barras proporcionais com labels e linha de média
- Carta com `contentType="table"` e `content.data` preenchido → exibe grid com header colorido e linhas de dados
- Unit com `type="stack"` no manifest → cria carta com Prev/Next buttons que flipam entre itens do stack, cada um com seu próprio contentType renderizado
- `manifest.json` exportado não contém `objectScores`, `totalAttempts`, `implementationNotes`, `conceptualOrigin`, `whyThisUnit`, `colorName`
- Cada carta construída via manifesto tem `VFXManager` + `CardClickVFXBridge` nos componentes; ao expandir dispara Lightning Aura por default
- Adicionar novo preset à `PaperCaveVFXLibrary.asset` no Inspector → disponível imediatamente para qualquer carta via `effectIndex`

---

## Guia de Teste — Como verificar cada melhoria

### Pré-requisito: compilar sem erros

1. Abra o Unity e aguarde a recompilação automática dos scripts.
2. O Console não deve ter erros (warnings de deprecação em TMPro são normais).
3. Se houver erro de `Newtonsoft.Json.Linq`, confirme que o pacote `Newtonsoft Json` está listado em `Packages/packages-lock.json` (já estava presente antes desta implementação).

---

### Step 3 — Verificar limpeza do manifest (Python)

1. No terminal, dentro de `PaperCave/`, rode o pipeline completo para qualquer paper:
   ```bash
   python -m utils.unity_export <paper_id>
   ```
2. Abra o `manifest.json` gerado em `Assets/PaperCaveData/<paper_id>/manifest.json`.
3. **Confirmar ausência** das chaves: `objectScores`, `totalAttempts`, `assembledFromMultipleAttempts`, `implementationNotes`, `flattenedFromStacks`, `conceptualOrigin`, `whyThisUnit`, `colorName` (dentro de `styleHint`).
4. **Confirmar presença** de stacks: se o `07_reviewer_output.json` continha units com `type="stack"`, eles devem aparecer no manifest com o campo `items` intacto (não achatados).

---

### Step 1 — Verificar chart e table dinâmicos

#### Chart (barras)

1. Crie ou edite um `manifest.json` de teste adicionando uma unit com este formato:
   ```json
   {
     "id": "test_chart",
     "type": "card",
     "priority": "secondary",
     "title": "Exemplo de Chart",
     "category": "result",
     "summary": "Teste de barras dinâmicas",
     "contentType": "chart",
     "styleHint": { "categoryColor": "#00D4FF" },
     "content": {
       "title": "Acurácia por Modelo",
       "description": "GPT-4 obteve a maior acurácia geral.",
       "chartType": "bar",
       "data": {
         "labels": ["GPT-4", "GPT-3.5", "Llama-3", "Mistral"],
         "values": [0.92, 0.84, 0.78, 0.71],
         "average": 0.8125,
         "unit": "acurácia"
       }
     }
   }
   ```
2. No Unity: **Tools → PaperCave → Build Cards From Manifest…**, selecione o manifest.
3. Clique na carta `test_chart` na cena para expandi-la.
4. **Esperado:** 4 barras proporcionais (GPT-4 maior), rótulos de valor acima de cada barra, rótulos de eixo abaixo, linha horizontal "avg 0.81", e a nota de descrição abaixo do gráfico.
5. **Fallback:** remova o campo `data` do JSON → carta deve exibir apenas o texto de `description`, sem erros.

#### Table (tabela)

1. Adicione ao mesmo manifest uma unit:
   ```json
   {
     "id": "test_table",
     "type": "card",
     "priority": "secondary",
     "title": "Exemplo de Tabela",
     "category": "metric",
     "summary": "Teste de tabela dinâmica",
     "contentType": "table",
     "styleHint": { "categoryColor": "#00FF88" },
     "content": {
       "title": "Comparativo de Latência",
       "description": "Média medida em 100 requisições.",
       "data": {
         "columns": ["Modelo", "Latência (ms)"],
         "rows": [
           ["GPT-4",    "320"],
           ["GPT-3.5",  "180"],
           ["Llama-3",  "95"],
           ["Mistral",  "110"]
         ]
       }
     }
   }
   ```
2. Rebuild e clique na carta `test_table`.
3. **Esperado:** cabeçalho "Modelo / Latência (ms)" em bold com a cor da categoria, 4 linhas de dados alinhadas, e a nota de descrição abaixo.
4. **Teste de 3 colunas:** adicione uma terceira coluna ao `columns` e às `rows` → colunas devem usar `<pos=40%>` e `<pos=75%>`.

---

### Step 2 — Verificar stacks

1. Adicione ao manifest uma unit com `type="stack"`:
   ```json
   {
     "id": "test_stack",
     "type": "stack",
     "priority": "secondary",
     "category": "method",
     "stackLabel": "Pipeline em 3 Fases",
     "styleHint": { "categoryColor": "#FFB800" },
     "items": [
       {
         "index": 0,
         "title": "Fase 1 — Coleta",
         "contentType": "text_panel",
         "content": { "description": "Dados coletados via scraping de artigos acadêmicos." }
       },
       {
         "index": 1,
         "title": "Fase 2 — Processamento",
         "contentType": "text_panel",
         "content": { "description": "Embeddings gerados com sentence-transformers." }
       },
       {
         "index": 2,
         "title": "Fase 3 — Inferência",
         "contentType": "text_panel",
         "content": { "description": "LLM consultado via RAG com top-5 chunks." }
       }
     ]
   }
   ```
2. Rebuild e clique na carta `test_stack`.
3. **Esperado:**
   - Face colapsada mostra "Pipeline em 3 Fases" e "▶ 3 cards — method".
   - Face expandida mostra o título "Fase 1 — Coleta" e o texto do item.
   - Dois botões físicos 3D (‹ PREV e NEXT ›) aparecem abaixo da carta.
   - Clicar **NEXT ›** mostra "Fase 2 — Processamento"; mais um clique mostra "Fase 3 — Inferência".
   - Clicar **NEXT ›** novamente cicla de volta para "Fase 1".
   - O contador "1 / 3" (depois "2 / 3", "3 / 3") atualiza corretamente.
4. **Teste de stack com chart/table como item:** substitua o `contentType` de um dos items por `"chart"` com `data` preenchido → o painel desse item deve exibir barras.

---

### Step 4 — Verificar VFX Library

#### Criar o asset (passo manual no Editor)

1. No Project panel do Unity, navegue até `Assets/PaperCave3D/`.
2. Clique com botão direito → **Create → PaperCave → VFX Library**.
3. Nomeie o asset `PaperCaveVFXLibrary` (o nome padrão já é esse).
4. No Inspector, clique no **+** da lista `Presets` e configure:
   - `presetName`: `Lightning Aura`
   - `prefab`: arraste um prefab VFX de `Assets/VFX/` (qualquer prefab de partícula disponível no projeto)
   - `destroyAfter`: `5`
   - `followTarget`: marcado

#### Verificar wiring automático

1. Rebuild o manifest (o loader já chama `WireVFX` ao final de `BuildCardFromData`).
2. Selecione qualquer carta na Hierarchy.
3. No Inspector, confirme a presença dos componentes `VFXManager` e `CardClickVFXBridge`.
4. **Em Play Mode:** clique numa carta para expandi-la → o efeito VFX configurado deve ser instanciado.
5. `CardClickVFXBridge.effectIndex = 0` por padrão — para usar outro preset, altere o índice no Inspector.

#### Se `PaperCaveVFXLibrary.asset` não existir

- `WireVFX` detecta `null` e pula silenciosamente (nenhum erro).
- Cartas são construídas normalmente, sem `VFXManager`.

---

### Checklist final

| Funcionalidade | Como testar | Resultado esperado |
|---|---|---|
| manifest sem campos desnecessários | Inspecionar manifest.json gerado | Ausência de `objectScores`, `whyThisUnit`, etc. |
| Stacks preservados no manifest | Checar manifest com stacks no reviewer output | Campo `items` presente com todos os sub-itens |
| Chart dinâmico | Carta com `data.labels` + `data.values` | Barras proporcionais + linha de média |
| Chart fallback | Carta chart sem `data` | Exibe `description` como texto |
| Table dinâmica | Carta com `data.columns` + `data.rows` | Grid com header colorido |
| Stack flip | Carta com `type="stack"` | Botões PREV/NEXT ciclam entre painéis |
| VFX wiring | Rebuild com VFXLibrary.asset presente | Inspector da carta tem VFXManager + Bridge |
| VFX ausente | Rebuild sem VFXLibrary.asset | Sem erro, carta funciona normalmente |
