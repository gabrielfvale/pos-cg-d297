using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BlendshapeFlowPanel : MonoBehaviour
{
    private string[] stateLabels = {
        "1. Blendshape Ativada",
        "2. FacialCameraRig Captura",
        "3. OpenFace Analisa AUs",
        "4. Mapa Gravado no Redis"
    };
    private string[] stateDescs = {
        "Personagem 3D com blendshape\nativada ao máximo (valor = 1.0)",
        "FacialCameraRig captura\nimagem frontal do personagem",
        "OpenFace detecta AUs:\nAU6 – Cheek Raiser: 0.82\nAU5 – Upper Lip Raiser: 0.316",
        "Mapa blendshape→AU gravado\nno Redis com modelID único"
    };

    private Image[] stateBoxes;
    private Text[] stateTexts;
    private Text detailText;
    private Button playBtn;
    private Text playBtnText;

    private int currentState = -1;
    private bool isPlaying = false;
    private Coroutine playCoroutine;

    private Color activeColor = new Color(0f, 0.83f, 1f);
    private Color inactiveColor = new Color(0f, 0.83f, 1f, 0.2f);

    void Start()
    {
        BuildPanel();
    }

    void BuildPanel()
    {
        var rt = GetComponent<RectTransform>();

        // Title
        CreateText("Blendshape → AU Mapping Flow", 22, activeColor, TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -55), new Vector2(-10, -5));
        CreateText("Processo de tradução: formato artístico → formato científico FACS", 13,
            new Color(0.6f, 0.85f, 1f), TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -80), new Vector2(-10, -55));

        stateBoxes = new Image[4];
        stateTexts = new Text[4];

        float boxW = (rt.rect.width - 60f) / 4f;
        float boxH = rt.rect.height * 0.38f;
        float startX = 30f - rt.rect.width * 0.5f;
        float boxY = rt.rect.height * 0.12f;

        for (int i = 0; i < 4; i++)
        {
            float cx = startX + i * (boxW + 8f) + boxW * 0.5f;

            // Arrow between boxes
            if (i > 0)
            {
                var arr = new GameObject("Arr_" + i);
                arr.transform.SetParent(transform, false);
                var arrRT = arr.AddComponent<RectTransform>();
                arrRT.anchoredPosition = new Vector2(cx - boxW * 0.5f - 4f, boxY);
                arrRT.sizeDelta = new Vector2(14, 10);
                var arrImg = arr.AddComponent<Image>();
                arrImg.color = new Color(1f, 1f, 1f, 0.6f);
            }

            var box = new GameObject("StateBox_" + i);
            box.transform.SetParent(transform, false);
            var boxRT = box.AddComponent<RectTransform>();
            boxRT.anchoredPosition = new Vector2(cx, boxY);
            boxRT.sizeDelta = new Vector2(boxW - 4f, boxH);

            var img = box.AddComponent<Image>();
            img.color = inactiveColor;
            stateBoxes[i] = img;

            var outline = box.AddComponent<Outline>();
            outline.effectColor = activeColor;
            outline.effectDistance = new Vector2(2, 2);

            // State number
            var numGO = new GameObject("Num");
            numGO.transform.SetParent(box.transform, false);
            var numRT = numGO.AddComponent<RectTransform>();
            numRT.anchorMin = new Vector2(0, 0.75f);
            numRT.anchorMax = new Vector2(1, 1);
            numRT.offsetMin = numRT.offsetMax = Vector2.zero;
            var numTxt = numGO.AddComponent<Text>();
            numTxt.text = (i + 1).ToString();
            numTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            numTxt.fontSize = 28;
            numTxt.color = activeColor;
            numTxt.alignment = TextAnchor.MiddleCenter;
            numTxt.fontStyle = FontStyle.Bold;

            // State label
            var lblGO = new GameObject("Lbl");
            lblGO.transform.SetParent(box.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0, 0);
            lblRT.anchorMax = new Vector2(1, 0.75f);
            lblRT.offsetMin = new Vector2(4, 4);
            lblRT.offsetMax = new Vector2(-4, -4);
            var lbl = lblGO.AddComponent<Text>();
            lbl.text = stateLabels[i];
            lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.fontSize = 12;
            lbl.color = Color.white;
            lbl.alignment = TextAnchor.UpperCenter;
            stateTexts[i] = lbl;

            int idx = i;
            var btn = box.AddComponent<Button>();
            btn.onClick.AddListener(() => ShowState(idx));
        }

        // Detail panel
        var detailGO = new GameObject("DetailPanel");
        detailGO.transform.SetParent(transform, false);
        var detailRT = detailGO.AddComponent<RectTransform>();
        detailRT.anchorMin = new Vector2(0.02f, 0.02f);
        detailRT.anchorMax = new Vector2(0.98f, 0.35f);
        detailRT.offsetMin = detailRT.offsetMax = Vector2.zero;
        var detailBg = detailGO.AddComponent<Image>();
        detailBg.color = new Color(0.03f, 0.1f, 0.2f, 0.95f);
        var detailOutline = detailGO.AddComponent<Outline>();
        detailOutline.effectColor = new Color(0f, 0.83f, 1f, 0.5f);
        detailOutline.effectDistance = new Vector2(2, 2);

        var detailTxtGO = new GameObject("DetailTxt");
        detailTxtGO.transform.SetParent(detailGO.transform, false);
        var dtRT = detailTxtGO.AddComponent<RectTransform>();
        dtRT.anchorMin = Vector2.zero;
        dtRT.anchorMax = Vector2.one;
        dtRT.offsetMin = new Vector2(12, 8);
        dtRT.offsetMax = new Vector2(-12, -8);
        detailText = detailTxtGO.AddComponent<Text>();
        detailText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailText.fontSize = 14;
        detailText.color = new Color(0.85f, 0.95f, 1f);
        detailText.alignment = TextAnchor.MiddleLeft;
        detailText.text = "▶ Pressione Play para ver o fluxo animado, ou clique em cada estado.";

        // Play button
        var btnGO = new GameObject("PlayBtn");
        btnGO.transform.SetParent(transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.35f, 0.37f);
        btnRT.anchorMax = new Vector2(0.65f, 0.52f);
        btnRT.offsetMin = btnRT.offsetMax = Vector2.zero;
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = activeColor * 0.25f;
        var outBtn = btnGO.AddComponent<Outline>();
        outBtn.effectColor = activeColor;
        outBtn.effectDistance = new Vector2(2, 2);
        playBtn = btnGO.AddComponent<Button>();
        playBtn.onClick.AddListener(TogglePlay);

        var btnTxtGO = new GameObject("BtnTxt");
        btnTxtGO.transform.SetParent(btnGO.transform, false);
        var btRT = btnTxtGO.AddComponent<RectTransform>();
        btRT.anchorMin = Vector2.zero;
        btRT.anchorMax = Vector2.one;
        btRT.offsetMin = btRT.offsetMax = Vector2.zero;
        playBtnText = btnTxtGO.AddComponent<Text>();
        playBtnText.text = "▶  PLAY";
        playBtnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        playBtnText.fontSize = 16;
        playBtnText.color = activeColor;
        playBtnText.alignment = TextAnchor.MiddleCenter;
        playBtnText.fontStyle = FontStyle.Bold;
    }

    void ShowState(int idx)
    {
        currentState = idx;
        for (int i = 0; i < stateBoxes.Length; i++)
            stateBoxes[i].color = i == idx ? new Color(activeColor.r * 0.3f, activeColor.g * 0.3f, activeColor.b * 0.3f, 0.9f) : inactiveColor;
        detailText.text = "<b><color=#00D4FF>" + stateLabels[idx] + "</color></b>\n" + stateDescs[idx];
    }

    void TogglePlay()
    {
        if (isPlaying)
        {
            isPlaying = false;
            if (playCoroutine != null) StopCoroutine(playCoroutine);
            playBtnText.text = "▶  PLAY";
        }
        else
        {
            isPlaying = true;
            playBtnText.text = "⏸  PAUSE";
            playCoroutine = StartCoroutine(PlaySequence());
        }
    }

    IEnumerator PlaySequence()
    {
        while (isPlaying)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!isPlaying) yield break;
                ShowState(i);
                yield return new WaitForSeconds(1.8f);
            }
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
