using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class FidelityChartPanel : MonoBehaviour
{
    private string[] emotions = { "Anger", "Happiness", "Surprise", "Sadness", "Disgust", "Fear", "Contempt" };
    private float[] scores = { 3.87f, 3.41f, 3.34f, 2.89f, 2.58f, 2.52f, 2.29f };
    private float overallMean = 2.99f;

    private Color barColor = new Color(0f, 1f, 0.53f);
    private Color bgColor = new Color(0.04f, 0.09f, 0.15f);
    private Color meanLineColor = new Color(1f, 0.72f, 0f);

    private Text infoText;
    private List<(RectTransform rt, int idx)> bars = new List<(RectTransform, int)>();

    private string[] notes = {
        "Melhor resultado — emoção mais reconhecível pelo LLM.",
        "Alta fidelidade — expressão bem mapeada nos blendshapes.",
        "Boa performance — amplitude da expressão facilita detecção.",
        "Resultado médio — expressão mais sutil, difícil de mapear.",
        "Abaixo da média — combinação de AUs complexa.",
        "Abaixo da média — AUs de medo similares às de surpresa.",
        "Emoção mais difícil — menor média de fidelidade de todas."
    };

    void Start()
    {
        BuildPanel();
    }

    void BuildPanel()
    {
        var rt = GetComponent<RectTransform>();

        // Title
        CreateText("Fidelidade & Duração — Resultados", 20, new Color(0f, 1f, 0.53f), TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -50), new Vector2(-10, -5));

        // Subtitle
        CreateText("Avaliação 0–5 por testadores humanos | Média geral: 2.99", 13,
            new Color(0.5f, 0.9f, 0.6f), TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -72), new Vector2(-10, -50));

        // Chart area
        float chartLeft = 0.12f, chartRight = 0.97f;
        float chartBottom = 0.32f, chartTop = 0.92f;

        // Chart background
        var chartBg = new GameObject("ChartBg");
        chartBg.transform.SetParent(transform, false);
        var chartBgRT = chartBg.AddComponent<RectTransform>();
        chartBgRT.anchorMin = new Vector2(chartLeft, chartBottom);
        chartBgRT.anchorMax = new Vector2(chartRight, chartTop);
        chartBgRT.offsetMin = chartBgRT.offsetMax = Vector2.zero;
        chartBg.AddComponent<Image>().color = new Color(0.02f, 0.06f, 0.12f, 0.8f);

        // Bars (horizontal)
        float maxScore = 5f;
        float rowHeight = 1f / emotions.Length;

        for (int i = 0; i < emotions.Length; i++)
        {
            float normalizedScore = scores[i] / maxScore;
            float yMin = chartBottom + (emotions.Length - 1 - i) * rowHeight * (chartTop - chartBottom) / (chartTop - chartBottom);

            // Bar
            var bar = new GameObject("Bar_" + emotions[i]);
            bar.transform.SetParent(transform, false);
            var barRT = bar.AddComponent<RectTransform>();

            float rowBot = chartBottom + (float)(emotions.Length - 1 - i) / emotions.Length * (chartTop - chartBottom);
            float rowTop = rowBot + (chartTop - chartBottom) / emotions.Length;
            float barPad = 0.008f;

            barRT.anchorMin = new Vector2(chartLeft + 0.005f, rowBot + barPad);
            barRT.anchorMax = new Vector2(chartLeft + (chartRight - chartLeft) * normalizedScore, rowTop - barPad);
            barRT.offsetMin = barRT.offsetMax = Vector2.zero;

            var barImg = bar.AddComponent<Image>();
            barImg.color = barColor * 0.7f;

            // Glow outline
            var barOutline = bar.AddComponent<Outline>();
            barOutline.effectColor = barColor;
            barOutline.effectDistance = new Vector2(1, 1);

            // Hover interaction
            var trigger = bar.AddComponent<EventTrigger>();
            int idx = i;
            var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener((_) => {
                barImg.color = barColor;
                ShowInfo(idx);
            });
            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((_) => {
                barImg.color = barColor * 0.7f;
            });
            trigger.triggers.Add(entryEnter);
            trigger.triggers.Add(entryExit);

            bars.Add((barRT, i));

            // Score label
            CreateText(scores[i].ToString("F2"), 13, Color.white, TextAnchor.MiddleLeft,
                new Vector2(chartLeft + (chartRight - chartLeft) * normalizedScore + 0.005f, rowBot + barPad),
                new Vector2(chartLeft + (chartRight - chartLeft) * normalizedScore + 0.06f, rowTop - barPad),
                Vector2.zero, Vector2.zero);

            // Emotion label (Y axis)
            CreateText(emotions[i], 12, new Color(0.8f, 1f, 0.85f), TextAnchor.MiddleRight,
                new Vector2(0f, rowBot + barPad),
                new Vector2(chartLeft - 0.005f, rowTop - barPad),
                Vector2.zero, Vector2.zero);
        }

        // Mean line
        var meanLine = new GameObject("MeanLine");
        meanLine.transform.SetParent(transform, false);
        var mlRT = meanLine.AddComponent<RectTransform>();
        float meanX = chartLeft + (chartRight - chartLeft) * (overallMean / maxScore);
        mlRT.anchorMin = new Vector2(meanX - 0.002f, chartBottom);
        mlRT.anchorMax = new Vector2(meanX + 0.002f, chartTop);
        mlRT.offsetMin = mlRT.offsetMax = Vector2.zero;
        var mlImg = meanLine.AddComponent<Image>();
        mlImg.color = meanLineColor;

        // Mean label
        CreateText("μ = 2.99", 12, meanLineColor, TextAnchor.LowerCenter,
            new Vector2(meanX - 0.06f, chartTop),
            new Vector2(meanX + 0.06f, chartTop + 0.05f),
            Vector2.zero, Vector2.zero);

        // Duration info section
        var durBg = new GameObject("DurBg");
        durBg.transform.SetParent(transform, false);
        var durRT = durBg.AddComponent<RectTransform>();
        durRT.anchorMin = new Vector2(0.02f, 0.02f);
        durRT.anchorMax = new Vector2(0.98f, 0.29f);
        durRT.offsetMin = durRT.offsetMax = Vector2.zero;
        durBg.AddComponent<Image>().color = new Color(0.02f, 0.06f, 0.12f, 0.9f);
        var durOutline = durBg.AddComponent<Outline>();
        durOutline.effectColor = new Color(0f, 0.8f, 0.4f, 0.4f);
        durOutline.effectDistance = new Vector2(2, 2);

        // Duration text
        var infoGO = new GameObject("InfoText");
        infoGO.transform.SetParent(durBg.transform, false);
        var infoRT = infoGO.AddComponent<RectTransform>();
        infoRT.anchorMin = Vector2.zero;
        infoRT.anchorMax = Vector2.one;
        infoRT.offsetMin = new Vector2(12, 6);
        infoRT.offsetMax = new Vector2(-12, -6);
        infoText = infoGO.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        infoText.fontSize = 13;
        infoText.color = new Color(0.75f, 1f, 0.85f);
        infoText.alignment = TextAnchor.MiddleLeft;
        infoText.text = "<b>Duração de Geração:</b>  min 3.4s  |  média 8.2s  |  max 25.3s\n" +
            "<color=#00FF88>Zona adequada: 3.4s – 9.4s</color>  →  Adequado para loading screens, não para tempo real.\n" +
            "<color=#FFB800>Hover nas barras para ver notas por emoção.</color>";
    }

    void ShowInfo(int idx)
    {
        infoText.text = "<b><color=#00FF88>" + emotions[idx] + " — " + scores[idx].ToString("F2") + " / 5.0</color></b>\n" +
            notes[idx] + "\n" +
            "<color=#888>Duração média geração: 8.2s | Personagem mais forte: Ja-Long (3.71) | Mais fraco: Asuna (2.30)</color>";
    }

    GameObject CreateText(string text, int size, Color color, TextAnchor anchor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = size;
        txt.color = color;
        txt.alignment = anchor;
        txt.resizeTextForBestFit = false;
        return go;
    }
}
