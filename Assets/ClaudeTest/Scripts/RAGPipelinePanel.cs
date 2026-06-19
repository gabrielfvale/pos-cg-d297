using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class RAGPipelinePanel : MonoBehaviour
{
    private string[] componentNames = {
        "Unity Plugin", "RAG App", "OpenFace", "Redis DB", "LLM Animator Agent"
    };
    private string[] componentDescs = {
        "Captura imagem do personagem 3D e envia para o pipeline RAG.",
        "Orquestra o fluxo: recebe prompt, consulta Redis, aciona LLM.",
        "Analisa imagem facial e detecta Action Units (FACS) com scores.",
        "Armazena mapeamento blendshape→AU por modelID para consulta rápida.",
        "Consome o mapa AU do Redis e gera animação JSON com os blendshapes."
    };
    private Color[] componentColors = {
        new Color(1f, 0.72f, 0f),
        new Color(1f, 0.6f, 0f),
        new Color(0f, 0.83f, 1f),
        new Color(0.2f, 0.8f, 0.4f),
        new Color(1f, 0.72f, 0f)
    };

    private GameObject tooltipPanel;
    private Text tooltipText;
    private List<GameObject> nodeBoxes = new List<GameObject>();
    private List<RectTransform> arrowRTs = new List<RectTransform>();
    private int pulseIndex = 0;
    private float pulseTimer = 0f;
    private float pulseInterval = 0.7f;

    public Sprite fig1Sprite;

    void Start()
    {
        BuildPanel();
        StartCoroutine(PulseArrows());
    }

    void BuildPanel()
    {
        var rt = GetComponent<RectTransform>();

        // Title
        CreateText("RAG Pipeline Architecture", 28, new Color(1f, 0.72f, 0f), TextAnchor.UpperCenter,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, rt.rect.height - 55), new Vector2(-10, -5));

        // Subtitle
        CreateText("Clique em cada componente para detalhes", 14, new Color(0.7f, 0.7f, 0.9f), TextAnchor.UpperCenter,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, rt.rect.height - 80), new Vector2(-10, -55));

        // If FIG1 sprite available, show it in background
        if (fig1Sprite != null)
        {
            var imgGO = new GameObject("FIG1_Background");
            imgGO.transform.SetParent(transform, false);
            var imgRT = imgGO.AddComponent<RectTransform>();
            imgRT.anchorMin = new Vector2(0.05f, 0.35f);
            imgRT.anchorMax = new Vector2(0.95f, 0.88f);
            imgRT.offsetMin = imgRT.offsetMax = Vector2.zero;
            var img = imgGO.AddComponent<Image>();
            img.sprite = fig1Sprite;
            img.preserveAspect = true;
            img.color = new Color(1, 1, 1, 0.85f);
        }
        else
        {
            BuildDiagramNodes(rt);
        }

        BuildTooltip(rt);
    }

    void BuildDiagramNodes(RectTransform rt)
    {
        float panelW = rt.rect.width;
        float panelH = rt.rect.height;

        float nodeW = (panelW - 80f) / 5f;
        float nodeH = 70f;
        float startX = 40f - panelW * 0.5f;
        float nodeY = -panelH * 0.1f;

        for (int i = 0; i < componentNames.Length; i++)
        {
            float cx = startX + i * (nodeW + 8f) + nodeW * 0.5f;

            // Arrow (except before first)
            if (i > 0)
            {
                var arrow = new GameObject("Arrow_" + i);
                arrow.transform.SetParent(transform, false);
                var aRT = arrow.AddComponent<RectTransform>();
                aRT.anchoredPosition = new Vector2(cx - nodeW * 0.5f - 4f, nodeY);
                aRT.sizeDelta = new Vector2(16f, 12f);
                var aImg = arrow.AddComponent<Image>();
                aImg.color = new Color(1f, 0.9f, 0.4f, 0.9f);
                arrowRTs.Add(aRT);
            }

            // Node box
            var box = new GameObject("Node_" + i);
            box.transform.SetParent(transform, false);
            var boxRT = box.AddComponent<RectTransform>();
            boxRT.anchoredPosition = new Vector2(cx, nodeY);
            boxRT.sizeDelta = new Vector2(nodeW - 4f, nodeH);

            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(componentColors[i].r * 0.2f, componentColors[i].g * 0.2f, componentColors[i].b * 0.2f, 0.9f);

            // Border outline
            var outline = box.AddComponent<Outline>();
            outline.effectColor = componentColors[i];
            outline.effectDistance = new Vector2(2, 2);

            // Label
            var lbl = new GameObject("NodeLabel");
            lbl.transform.SetParent(box.transform, false);
            var lblRT = lbl.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(4, 4);
            lblRT.offsetMax = new Vector2(-4, -4);
            var lblTxt = lbl.AddComponent<Text>();
            lblTxt.text = componentNames[i];
            lblTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lblTxt.fontSize = 13;
            lblTxt.color = componentColors[i];
            lblTxt.alignment = TextAnchor.MiddleCenter;
            lblTxt.fontStyle = FontStyle.Bold;

            int idx = i;
            var btn = box.AddComponent<Button>();
            btn.onClick.AddListener(() => ShowTooltip(idx));
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, 1, 1, 1.3f);
            btn.colors = colors;

            nodeBoxes.Add(box);
        }
    }

    void BuildTooltip(RectTransform rt)
    {
        tooltipPanel = new GameObject("Tooltip");
        tooltipPanel.transform.SetParent(transform, false);
        var tRT = tooltipPanel.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.05f, 0.02f);
        tRT.anchorMax = new Vector2(0.95f, 0.30f);
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        var bg = tooltipPanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.18f, 0.95f);

        var outline = tooltipPanel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.72f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2, 2);

        var txtGO = new GameObject("TooltipText");
        txtGO.transform.SetParent(tooltipPanel.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(12, 8);
        txtRT.offsetMax = new Vector2(-12, -8);
        tooltipText = txtGO.AddComponent<Text>();
        tooltipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tooltipText.fontSize = 15;
        tooltipText.color = new Color(0.9f, 0.9f, 1f);
        tooltipText.alignment = TextAnchor.MiddleLeft;
        tooltipText.text = "← Clique em um componente para ver sua descrição no pipeline.";

        tooltipPanel.SetActive(true);
    }

    void ShowTooltip(int idx)
    {
        tooltipText.text = "<b><color=#FFB800>" + componentNames[idx] + "</color></b>\n" + componentDescs[idx];
        tooltipPanel.SetActive(true);
    }

    IEnumerator PulseArrows()
    {
        while (true)
        {
            yield return new WaitForSeconds(pulseInterval);
            for (int i = 0; i < arrowRTs.Count; i++)
            {
                bool active = i == pulseIndex % arrowRTs.Count;
                var img = arrowRTs[i].GetComponent<Image>();
                if (img != null)
                    img.color = active ? new Color(1f, 0.85f, 0.1f, 1f) : new Color(1f, 0.72f, 0f, 0.35f);
            }
            pulseIndex++;
        }
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
        return go;
    }
}
