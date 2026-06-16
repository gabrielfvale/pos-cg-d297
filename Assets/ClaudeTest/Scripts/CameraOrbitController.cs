using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CameraOrbitController : MonoBehaviour
{
    public int panelCount = 5;
    public float radius = 8f;
    public float rotationDuration = 0.5f;

    private int currentIndex = 0;
    private bool isRotating = false;
    private float[] panelAngles;

    // UI
    private Canvas hudCanvas;
    private Text panelNameText;
    private Image[] dotImages;
    private string[] panelNames = {
        "01 — RAG Pipeline Architecture",
        "02 — Blendshape→AU Mapping Flow",
        "03 — Fidelidade & Duração",
        "04 — Demo App & Character Gallery",
        "05 — Limitação: Não é Real-Time"
    };

    void Start()
    {
        panelAngles = new float[panelCount];
        for (int i = 0; i < panelCount; i++)
            panelAngles[i] = i * (360f / panelCount);

        // Set initial camera rotation to face panel 0
        transform.rotation = Quaternion.Euler(0, panelAngles[0], 0);

        BuildHUD();
        UpdateLabel();
    }

    void BuildHUD()
    {
        var hudGO = new GameObject("HUD_Canvas");
        hudCanvas = hudGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 10;
        hudGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        hudGO.AddComponent<GraphicRaycaster>();

        Color btnColor = new Color(0.08f, 0.12f, 0.22f, 0.9f);
        Color borderColor = new Color(0.4f, 0.5f, 0.8f, 0.9f);

        // Left arrow button
        CreateArrowButton(hudGO.transform, "◀", new Vector2(60, -50), new Vector2(0, 1), new Vector2(0, 1), () => Navigate(-1), btnColor, borderColor);

        // Right arrow button
        CreateArrowButton(hudGO.transform, "▶", new Vector2(-60, -50), new Vector2(1, 1), new Vector2(1, 1), () => Navigate(1), btnColor, borderColor);

        // Panel name label at bottom
        var lblGO = new GameObject("PanelNameLabel");
        lblGO.transform.SetParent(hudGO.transform, false);
        var lblRT = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin = new Vector2(0.15f, 0f);
        lblRT.anchorMax = new Vector2(0.85f, 0f);
        lblRT.pivot = new Vector2(0.5f, 0f);
        lblRT.anchoredPosition = new Vector2(0, 18f);
        lblRT.sizeDelta = new Vector2(0, 36f);

        var lblBg = lblGO.AddComponent<Image>();
        lblBg.color = new Color(0.04f, 0.07f, 0.15f, 0.88f);

        var lblOut = lblGO.AddComponent<Outline>();
        lblOut.effectColor = new Color(0.3f, 0.4f, 0.7f, 0.7f);
        lblOut.effectDistance = new Vector2(2, 2);

        var txtGO = new GameObject("LblTxt");
        txtGO.transform.SetParent(lblGO.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(12, 0);
        txtRT.offsetMax = new Vector2(-12, 0);
        panelNameText = txtGO.AddComponent<Text>();
        panelNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panelNameText.fontSize = 16;
        panelNameText.color = new Color(0.75f, 0.82f, 1f);
        panelNameText.alignment = TextAnchor.MiddleCenter;
        panelNameText.fontStyle = FontStyle.Bold;

        // Dot indicators
        dotImages = BuildDotIndicators(hudGO.transform);
    }

    void CreateArrowButton(Transform parent, string label, Vector2 anchoredPos, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action, Color bg, Color border)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(56, 56);

        var img = go.AddComponent<Image>();
        img.color = bg;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(2, 2);

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(action);
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.4f, 1.4f, 1.8f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.9f);
        btn.colors = colors;

        var txtGO = new GameObject("ArrowTxt");
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 26;
        txt.color = new Color(0.7f, 0.8f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;
    }

    Image[] BuildDotIndicators(Transform parent)
    {
        var dotContainer = new GameObject("DotIndicators");
        dotContainer.transform.SetParent(parent, false);
        var dcRT = dotContainer.AddComponent<RectTransform>();
        dcRT.anchorMin = new Vector2(0.5f, 0f);
        dcRT.anchorMax = new Vector2(0.5f, 0f);
        dcRT.pivot = new Vector2(0.5f, 0f);
        dcRT.anchoredPosition = new Vector2(0, 60f);
        dcRT.sizeDelta = new Vector2(panelCount * 22f, 14f);

        var hlg = dotContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var imgs = new Image[panelCount];
        for (int i = 0; i < panelCount; i++)
        {
            var dot = new GameObject("Dot_" + i);
            dot.transform.SetParent(dotContainer.transform, false);
            var dotRT = dot.AddComponent<RectTransform>();
            dotRT.sizeDelta = new Vector2(10, 10);
            imgs[i] = dot.AddComponent<Image>();
            imgs[i].color = i == 0 ? new Color(0.6f, 0.75f, 1f) : new Color(0.2f, 0.25f, 0.4f);
        }
        return imgs;
    }

    void UpdateLabel()
    {
        if (panelNameText != null)
            panelNameText.text = panelNames[currentIndex];

        if (dotImages == null) return;
        for (int i = 0; i < dotImages.Length; i++)
            if (dotImages[i] != null)
                dotImages[i].color = i == currentIndex ? new Color(0.6f, 0.75f, 1f) : new Color(0.2f, 0.25f, 0.4f);
    }

    public void Navigate(int direction)
    {
        if (isRotating) return;
        currentIndex = (currentIndex + direction + panelCount) % panelCount;
        StartCoroutine(RotateTo(panelAngles[currentIndex]));
        UpdateLabel();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            Navigate(1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            Navigate(-1);
    }

    IEnumerator RotateTo(float targetAngle)
    {
        isRotating = true;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.Euler(0, targetAngle, 0);

        // Always take shortest path
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / rotationDuration;
            transform.rotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        transform.rotation = endRot;
        isRotating = false;
    }
}
