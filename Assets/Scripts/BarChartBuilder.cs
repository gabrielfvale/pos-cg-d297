using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Constrói um gráfico de barras UI dinamicamente — espelho do TableBuilder.
/// Os dados são os mesmos da tabela (personagens com Facial Blend Shapes).
/// Cada barra recebe um BarChartBarHover (equivalente ao TableRowHover).
/// Chamado por JsonSceneInstantiator da mesma forma que TableBuilder.Build().
/// </summary>
public class BarChartBuilder : MonoBehaviour
{
    // ── Dados (mesmos da tabela) ─────────────────────────────────────────────
    private static readonly string[] CharacterNames  = { "Alina", "Asuna", "Atticus\n(G2)", "Ja-Long\n(G2)", "Zaniyah\n(G2)", "Disa\n(G3)", "Khalan\n(G8)" };
    private static readonly int[]    BlendShapes     = { 174, 52, 146, 146, 140, 130, 249 };

    // ── Paleta ───────────────────────────────────────────────────────────────
    private static readonly Color ColBar     = new Color(0.25f, 0.50f, 0.85f, 1f);
    private static readonly Color ColBg      = new Color(0.10f, 0.12f, 0.17f, 1f);
    private static readonly Color ColGrid    = new Color(1f,    1f,    1f,    0.07f);
    private static readonly Color ColAxis    = new Color(1f,    1f,    1f,    0.35f);
    private static readonly Color ColText    = new Color(0.88f, 0.91f, 0.95f, 1f);
    private static readonly Color ColHeader  = new Color(0.15f, 0.35f, 0.60f, 1f);

    // ── Layout (mesmas 30×30 unidades da tabela) ─────────────────────────────
    private const float W          = 30f;
    private const float H          = 30f;
    private const float PadL       = 5.0f;
    private const float PadR       = 1.0f;
    private const float PadT       = 3.0f;
    private const float PadB       = 6.0f;
    private const float BarGap     = 0.4f;   // espaço entre barras

    // ── Troca de Imagem (igual ao TableBuilder) ────────────────────────────────
    [Header("Troca de Imagem por Hover")]
    [Tooltip("Nome exato do GameObject de Imagem na cena controlado pelo hover")]
    public string   imageTargetName = "ExemplosExpressoesGeradas";
    [Tooltip("Um sprite por barra — índice 0 = barra 1, etc.")]
    public Sprite[] rowSprites = new Sprite[7];

    // ── Estado ───────────────────────────────────────────────────────────────
    private readonly List<BarChartBarHover> _bars = new();

    // ── API pública (chamada pelo JsonSceneInstantiator) ─────────────────────
    public void Build()
    {
        // Limpar filhos anteriores
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        _bars.Clear();

        // RectTransform raiz
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(W, H);

        // Fundo geral
        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = ColBg;

        float plotW = W - PadL - PadR;
        float plotH = H - PadT - PadB;
        int   n     = CharacterNames.Length;
        int   maxV  = 280;

        // ── Eixos ────────────────────────────────────────────────────────────
        CreateRect("AxisX", new Vector2(PadL, PadB),         new Vector2(plotW, 0.08f), ColAxis);
        CreateRect("AxisY", new Vector2(PadL, PadB),         new Vector2(0.08f, plotH), ColAxis);

        // ── Grid + rótulos Y ─────────────────────────────────────────────────
        int steps = 4;
        for (int g = 0; g <= steps; g++)
        {
            float frac = (float)g / steps;
            float y    = PadB + frac * plotH;
            int   val  = Mathf.RoundToInt(frac * maxV);

            if (g > 0)
                CreateRect($"Grid_{g}", new Vector2(PadL, y), new Vector2(plotW, 0.05f), ColGrid);

            CreateLabel($"YLbl_{g}",
                new Vector2(0.1f, y - 0.8f), new Vector2(PadL - 0.3f, 1.6f),
                val.ToString(), ColAxis, 1.8f, TextAlignmentOptions.Right);
        }

        // ── Cabeçalho (fundo azul + título — idêntico à célula de header da tabela) ──
        var headerBg = CreateRect("Header",
            new Vector2(0f, H - PadT), new Vector2(W, PadT), ColHeader);

        CreateLabel("Title",
            new Vector2(0f, H - PadT + 0.3f), new Vector2(W, PadT - 0.3f),
            "Facial Blend Shapes",
            Color.white, 2f, TextAlignmentOptions.Center, FontStyles.Bold);

        // Rótulo eixo Y vertical
        CreateLabel("YAxisTitle",
            new Vector2(0.1f, PadB + plotH * 0.3f), new Vector2(1.5f, plotH * 0.4f),
            "Blend\nShapes", ColAxis, 1.6f, TextAlignmentOptions.Center);

        // ── Barras ───────────────────────────────────────────────────────────
        float totalBarW = plotW - BarGap * (n + 1);
        float barW      = totalBarW / n;

        for (int i = 0; i < n; i++)
        {
            float frac   = (float)BlendShapes[i] / maxV;
            float barH   = frac * plotH;
            float x      = PadL + BarGap * (i + 1) + barW * i;
            float y      = PadB;

            // Container da barra (receberá o BarChartBarHover e o BoxCollider)
            var barGO = CreateBarContainer($"Bar_{i}",
                new Vector2(x, y), new Vector2(barW, barH),
                ColBar);

            // Valor acima da barra
            CreateLabel($"Val_{i}",
                new Vector2(x, y + barH + 0.15f), new Vector2(barW, 1.5f),
                BlendShapes[i].ToString(),
                Color.white, 2.0f, TextAlignmentOptions.Center, FontStyles.Bold);

            // Nome abaixo da barra
            CreateLabel($"Name_{i}",
                new Vector2(x - 0.3f, 0.2f), new Vector2(barW + 0.6f, PadB - 0.3f),
                CharacterNames[i],
                ColText, 2.0f, TextAlignmentOptions.Center);

            // BarChartBarHover — equivalente ao TableRowHover
            var hover              = barGO.AddComponent<BarChartBarHover>();
            hover.barIndex         = i;
            hover.imageTargetName  = imageTargetName;
            hover.rowSprites       = rowSprites;
            hover.SetOrigin(barGO.transform.localPosition);

            _bars.Add(hover);
        }
    }

    // ── Callbacks de hover chamados pelos filhos ──────────────────────────────
    public void OnBarEnterHover(int barIndex)
    {
        for (int i = 0; i < _bars.Count; i++)
            _bars[i].ApplyHighlight(i == barIndex, true);
    }

    public void OnBarExitHover(int barIndex)
    {
        for (int i = 0; i < _bars.Count; i++)
            _bars[i].ApplyHighlight(false, false);
    }

    // ── Helpers de criação UI ─────────────────────────────────────────────────

    /// <summary>Cria um retângulo UI simples (Image) sem hover.</summary>
    private Image CreateRect(string name, Vector2 pos, Vector2 size, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(transform, false);

        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = Vector2.zero;
        rt.anchorMax    = Vector2.zero;
        rt.pivot        = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta    = size;

        var img  = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    /// <summary>
    /// Cria o container de uma barra — tem Image + Canvas (para sortOrder) +
    /// BoxCollider (necessário para o Raycast do BarChartBarHover).
    /// </summary>
    private GameObject CreateBarContainer(string name, Vector2 pos, Vector2 size, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(transform, false);

        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = Vector2.zero;
        rt.anchorMax    = Vector2.zero;
        rt.pivot        = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta    = size;

        var img   = go.AddComponent<Image>();
        img.color = color;

        // BoxCollider será redimensionado pelo BarChartBarHover.SetOrigin()
        go.AddComponent<BoxCollider>();

        return go;
    }

    /// <summary>Cria um label TextMeshProUGUI.</summary>
    private void CreateLabel(string name, Vector2 pos, Vector2 size,
        string text, Color color, float fontSize,
        TextAlignmentOptions align = TextAlignmentOptions.Center,
        FontStyles style           = FontStyles.Normal)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(transform, false);

        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = Vector2.zero;
        rt.anchorMax    = Vector2.zero;
        rt.pivot        = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta    = size;

        var tmp              = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.color            = color;
        tmp.fontStyle        = style;
        tmp.alignment        = align;
        tmp.overflowMode     = TextOverflowModes.Overflow;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = 0.5f;
        tmp.fontSizeMax      = fontSize;
    }
}
