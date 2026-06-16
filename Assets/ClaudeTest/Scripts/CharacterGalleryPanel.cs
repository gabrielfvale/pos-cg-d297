using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CharacterGalleryPanel : MonoBehaviour
{
    private string[] characters = { "Alina", "Asuna", "Atticus", "Disa", "Ja-Long", "Zaniyah", "Khalan" };
    private string[] emotions = { "happiness", "anger", "sadness", "fear", "contempt", "disgust", "surprise" };
    private float[] fidelities = { 2.95f, 2.30f, 2.85f, 2.60f, 3.71f, 2.75f, 3.10f };
    private int[] blendshapes = { 42, 36, 55, 48, 61, 44, 39 };

    private Color[] emotionColors = {
        new Color(1f, 0.9f, 0.2f),
        new Color(1f, 0.3f, 0.2f),
        new Color(0.3f, 0.5f, 1f),
        new Color(0.7f, 0.3f, 1f),
        new Color(1f, 0.5f, 0f),
        new Color(0.3f, 0.9f, 0.3f),
        new Color(0f, 0.9f, 1f)
    };

    private Text infoText;
    public Sprite fig3Sprite;

    void Start()
    {
        BuildPanel();
    }

    void BuildPanel()
    {
        var rt = GetComponent<RectTransform>();

        // Title
        CreateText("Demo App & Character Gallery", 22, Color.white, TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -50), new Vector2(-10, -5));
        CreateText("MoodMorph Demo — 7 personagens, 7 emoções testadas", 13,
            new Color(0.85f, 0.85f, 0.95f), TextAnchor.UpperCenter,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -70), new Vector2(-10, -50));

        if (fig3Sprite != null)
        {
            var figGO = new GameObject("FIG3");
            figGO.transform.SetParent(transform, false);
            var figRT = figGO.AddComponent<RectTransform>();
            figRT.anchorMin = new Vector2(0.03f, 0.38f);
            figRT.anchorMax = new Vector2(0.97f, 0.90f);
            figRT.offsetMin = figRT.offsetMax = Vector2.zero;
            var figImg = figGO.AddComponent<Image>();
            figImg.sprite = fig3Sprite;
            figImg.preserveAspect = true;
        }
        else
        {
            BuildCharacterGrid(rt);
        }

        BuildInfoPanel();
    }

    void BuildCharacterGrid(RectTransform rt)
    {
        float gridTop = 0.90f, gridBottom = 0.36f;
        float cellW = 1f / characters.Length;
        float cellH = gridTop - gridBottom;

        for (int i = 0; i < characters.Length; i++)
        {
            float xMin = i * cellW + 0.005f;
            float xMax = xMin + cellW - 0.01f;

            // Face circle (primitive representation)
            var faceGO = new GameObject("Face_" + characters[i]);
            faceGO.transform.SetParent(transform, false);
            var faceRT = faceGO.AddComponent<RectTransform>();
            faceRT.anchorMin = new Vector2(xMin + 0.01f, gridBottom + cellH * 0.35f);
            faceRT.anchorMax = new Vector2(xMax - 0.01f, gridTop - 0.02f);
            faceRT.offsetMin = faceRT.offsetMax = Vector2.zero;

            var faceImg = faceGO.AddComponent<Image>();
            faceImg.color = emotionColors[i] * 0.25f;
            var faceOutline = faceGO.AddComponent<Outline>();
            faceOutline.effectColor = emotionColors[i];
            faceOutline.effectDistance = new Vector2(2, 2);

            // Emoji / letter representing face
            var emojiGO = new GameObject("FaceIcon");
            emojiGO.transform.SetParent(faceGO.transform, false);
            var emojiRT = emojiGO.AddComponent<RectTransform>();
            emojiRT.anchorMin = new Vector2(0.1f, 0.3f);
            emojiRT.anchorMax = new Vector2(0.9f, 0.95f);
            emojiRT.offsetMin = emojiRT.offsetMax = Vector2.zero;
            var emojiTxt = emojiGO.AddComponent<Text>();
            emojiTxt.text = characters[i].Substring(0, 1);
            emojiTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            emojiTxt.fontSize = 36;
            emojiTxt.color = emotionColors[i];
            emojiTxt.alignment = TextAnchor.MiddleCenter;
            emojiTxt.fontStyle = FontStyle.Bold;

            // Emotion label
            var emGO = new GameObject("EmLabel");
            emGO.transform.SetParent(faceGO.transform, false);
            var emRT = emGO.AddComponent<RectTransform>();
            emRT.anchorMin = new Vector2(0, 0.05f);
            emRT.anchorMax = new Vector2(1, 0.32f);
            emRT.offsetMin = emRT.offsetMax = Vector2.zero;
            var emTxt = emGO.AddComponent<Text>();
            emTxt.text = emotions[i];
            emTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            emTxt.fontSize = 11;
            emTxt.color = emotionColors[i];
            emTxt.alignment = TextAnchor.MiddleCenter;

            // Name label below
            CreateText(characters[i], 12, Color.white, TextAnchor.UpperCenter,
                new Vector2(xMin, gridBottom + 0.02f),
                new Vector2(xMax, gridBottom + cellH * 0.33f),
                Vector2.zero, Vector2.zero);

            int idx = i;
            var btn = faceGO.AddComponent<Button>();
            btn.onClick.AddListener(() => ShowCharacterInfo(idx));
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(1, 1, 1, 1.2f);
            btn.colors = btnColors;
        }
    }

    void BuildInfoPanel()
    {
        var infoBg = new GameObject("InfoBg");
        infoBg.transform.SetParent(transform, false);
        var infoRT = infoBg.AddComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.02f, 0.02f);
        infoRT.anchorMax = new Vector2(0.98f, 0.33f);
        infoRT.offsetMin = infoRT.offsetMax = Vector2.zero;
        infoBg.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.12f, 0.95f);
        var infoOutline = infoBg.AddComponent<Outline>();
        infoOutline.effectColor = new Color(1f, 1f, 1f, 0.3f);
        infoOutline.effectDistance = new Vector2(2, 2);

        var txtGO = new GameObject("InfoText");
        txtGO.transform.SetParent(infoBg.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(12, 6);
        txtRT.offsetMax = new Vector2(-12, -6);
        infoText = txtGO.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        infoText.fontSize = 13;
        infoText.color = new Color(0.9f, 0.9f, 1f);
        infoText.alignment = TextAnchor.MiddleLeft;
        infoText.text = "← Clique em um personagem para ver fidelidade média e detalhes do modelo.\n" +
            "Interface MoodMorph: campos Character, Facial Expression, Blendshape-AU Confidence.";
    }

    void ShowCharacterInfo(int idx)
    {
        infoText.text = string.Format(
            "<b><color=#FFFFFF>{0}</color></b>  —  Emoção: <color=#{6}>{1}</color>\n" +
            "Fidelidade média: <b>{2:F2} / 5.0</b>   |   Blendshapes no modelo: <b>{3}</b>\n" +
            "Variação: personagens com mais blendshapes tendem a ter maior fidelidade.",
            characters[idx], emotions[idx], fidelities[idx], blendshapes[idx],
            "", "", ColorUtility.ToHtmlStringRGB(emotionColors[idx]));
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
