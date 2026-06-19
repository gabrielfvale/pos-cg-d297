using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class LimitationPanel : MonoBehaviour, IPointerClickHandler
{
    private bool isExpanded = false;
    private CanvasGroup cg;
    private Text mainText;
    private GameObject expandedSection;
    private float trembleAmount = 3f;
    private RectTransform rt;

    void Start()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0.6f;

        BuildPanel();
        StartCoroutine(TrembleIdle());
    }

    void BuildPanel()
    {
        // Title
        CreateText("Limitação: Não é Real-Time", 20, new Color(0.7f, 0.78f, 0.9f), TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -50), new Vector2(-10, -5));

        CreateText("Clique para expandir → Future Work", 12, new Color(0.5f, 0.6f, 0.75f), TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -70), new Vector2(-10, -50));

        // Quote 1
        var q1 = new GameObject("Quote1Bg");
        q1.transform.SetParent(transform, false);
        var q1RT = q1.AddComponent<RectTransform>();
        q1RT.anchorMin = new Vector2(0.03f, 0.54f);
        q1RT.anchorMax = new Vector2(0.97f, 0.82f);
        q1RT.offsetMin = q1RT.offsetMax = Vector2.zero;
        q1.AddComponent<Image>().color = new Color(0.15f, 0.18f, 0.28f, 0.8f);
        var q1Out = q1.AddComponent<Outline>();
        q1Out.effectColor = new Color(0.5f, 0.6f, 0.8f, 0.5f);
        q1Out.effectDistance = new Vector2(1, 1);

        CreateTextInParent(q1.transform,
            "\" RAG is not yet suitable for instant, real-time animation generation — it can be effectively utilized during level loading phases. \"",
            14, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8));

        // Quote 2
        var q2 = new GameObject("Quote2Bg");
        q2.transform.SetParent(transform, false);
        var q2RT = q2.AddComponent<RectTransform>();
        q2RT.anchorMin = new Vector2(0.03f, 0.27f);
        q2RT.anchorMax = new Vector2(0.97f, 0.52f);
        q2RT.offsetMin = q2RT.offsetMax = Vector2.zero;
        q2.AddComponent<Image>().color = new Color(0.15f, 0.18f, 0.28f, 0.8f);
        var q2Out = q2.AddComponent<Outline>();
        q2Out.effectColor = new Color(0.5f, 0.6f, 0.8f, 0.5f);
        q2Out.effectDistance = new Vector2(1, 1);

        CreateTextInParent(q2.transform,
            "\" The quantity and quality of a character's blend shapes are key factors in determining the accuracy of blend shape mappings and animation quality. \"",
            13, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8));

        // Editorial summary
        CreateText("O sistema abre o caminho, mas ainda não fecha a lacuna para uso em produção.",
            13, new Color(0.65f, 0.72f, 0.85f), TextAnchor.UpperCenter,
            new Vector2(0.03f, 0.13f), new Vector2(0.97f, 0.25f), Vector2.zero, Vector2.zero);

        // Expanded future work (hidden initially)
        expandedSection = new GameObject("FutureWork");
        expandedSection.transform.SetParent(transform, false);
        var fwRT = expandedSection.AddComponent<RectTransform>();
        fwRT.anchorMin = new Vector2(0.03f, 0.02f);
        fwRT.anchorMax = new Vector2(0.97f, 0.12f);
        fwRT.offsetMin = fwRT.offsetMax = Vector2.zero;
        expandedSection.AddComponent<Image>().color = new Color(0.1f, 0.15f, 0.25f, 0.9f);
        var fwOut = expandedSection.AddComponent<Outline>();
        fwOut.effectColor = new Color(0.4f, 0.55f, 0.8f, 0.8f);
        fwOut.effectDistance = new Vector2(2, 2);

        CreateTextInParent(expandedSection.transform,
            "<b>Future Work:</b>  • Otimização do mapeamento AU   • Cache multinível   • Modelos híbridos leve+pesado",
            13, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));

        expandedSection.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        isExpanded = !isExpanded;
        expandedSection.SetActive(isExpanded);
        StopCoroutine(TrembleIdle());

        if (isExpanded)
        {
            cg.alpha = 1f;
            StartCoroutine(StabilizePanel());
        }
        else
        {
            cg.alpha = 0.6f;
            StartCoroutine(TrembleIdle());
        }
    }

    IEnumerator TrembleIdle()
    {
        while (!isExpanded)
        {
            float t = Time.time;
            float x = Mathf.Sin(t * 7.3f) * trembleAmount;
            float y = Mathf.Cos(t * 5.1f) * trembleAmount * 0.5f;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x + x * Time.deltaTime,
                                              rt.anchoredPosition.y + y * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator StabilizePanel()
    {
        yield return new WaitForSeconds(0.3f);
    }

    GameObject CreateText(string text, int size, Color color, TextAnchor anchor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        return CreateTextInParent(transform, text, size, color, anchor, anchorMin, anchorMax, offsetMin, offsetMax);
    }

    GameObject CreateTextInParent(Transform parent, string text, int size, Color color, TextAnchor anchor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(parent, false);
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
