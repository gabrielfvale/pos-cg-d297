using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class PanelBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Style")]
    public Color borderColor = Color.white;
    public float glowIntensity = 1.5f;

    protected CanvasGroup canvasGroup;
    protected Image borderImage;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(FadeGlow(glowIntensity));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(FadeGlow(1f));
    }

    IEnumerator FadeGlow(float target)
    {
        if (borderImage == null) yield break;
        Color start = borderImage.color;
        Color end = borderColor * target;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            borderImage.color = Color.Lerp(start, end, t);
            yield return null;
        }
    }

    protected GameObject CreateLabel(Transform parent, string text, int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Label_" + text.Substring(0, Mathf.Min(8, text.Length)));
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.resizeTextForBestFit = false;
        return go;
    }
}
