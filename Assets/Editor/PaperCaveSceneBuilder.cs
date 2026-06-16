using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PaperCave;

/// <summary>
/// Builds the "PaperCave_Cards_2D" scene: a Screen-Space Overlay Canvas of
/// interactive, draggable, expandable cards generated from the Card Manifest.
/// Run via menu: Tools > PaperCave > Build PaperCave_Cards_2D Scene.
/// </summary>
public static class PaperCaveSceneBuilder
{
    // ---- Palette ---------------------------------------------------------
    static readonly Color BgColor    = Hex("#0A1628");
    static readonly Color PanelColor = Hex("#122441");
    static readonly Color White      = Color.white;

    const string FigureFolder = "Assets/Paper Figures/";
    const string ScenePath    = "Assets/Scenes/PaperCave_Cards_2D.unity";

    static Font _font;

    [MenuItem("Tools/PaperCave/Build PaperCave_Cards_2D Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera + Light (per project conventions).
        var camGO = new GameObject("Main Camera", typeof(Camera));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;
        camGO.transform.position = new Vector3(0f, 0f, -10f);

        var lightGO = new GameObject("Directional Light", typeof(Light));
        lightGO.GetComponent<Light>().type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // EventSystem with the correct input module for this project.
        var esGO = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif

        // Canvas (Screen Space - Overlay, 1920x1080 reference).
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Full-screen dark blue background.
        var bg = AddImage(canvasGO.transform, "Background", BgColor);
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;

        // Paper title header (non-interactive context label).
        var header = AddText(canvasGO.transform, "PaperTitle",
            "Generative AI for Facial Expressions in 3D Game Characters — A Retrieval-Augmented Approach",
            18, Alpha(White, 0.85f), FontStyle.Bold, TextAnchor.MiddleCenter, true);
        var hrt = header.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
        hrt.offsetMin = new Vector2(80f, -70f); hrt.offsetMax = new Vector2(-80f, -20f);
        header.raycastTarget = false;

        // ---- Cards -------------------------------------------------------
        BuildCard01(canvasGO.transform, new Vector2(-580f, 210f));
        BuildCard02(canvasGO.transform, new Vector2(-260f, 210f));
        BuildCard03(canvasGO.transform, new Vector2(  30f, 210f));
        BuildCard04(canvasGO.transform, new Vector2( 320f, 210f));
        BuildCard05(canvasGO.transform, new Vector2( 610f, 210f));

        // ---- Save --------------------------------------------------------
        string absDir = System.IO.Path.GetDirectoryName(
            System.IO.Path.Combine(Application.dataPath, "../" + ScenePath));
        System.IO.Directory.CreateDirectory(absDir);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("PaperCave: scene built and saved to " + ScenePath);
    }

    // =====================================================================
    //  CARD SHELL
    // =====================================================================

    class CardParts
    {
        public GameObject root;
        public RectTransform rt;
        public GameObject collapsed;
        public GameObject expanded; // fills the card; header already placed
    }

    static CardParts CreateCard(Transform parent, string name, float width, float height,
        Vector2 pos, string category, Color catColor, string title, string summary)
    {
        var root = NewUI(name, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f); // top pivot -> grows downward
        rt.sizeDelta = new Vector2(width, 80f);
        rt.anchoredPosition = pos;

        var img = root.AddComponent<Image>();
        img.color = PanelColor;

        var button = root.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        var outline = root.AddComponent<Outline>();
        outline.effectColor = catColor;
        outline.effectDistance = new Vector2(2f, 2f);

        root.AddComponent<CardDragHandler>();

        // Collapsed view (header + summary).
        var collapsed = NewUI("CollapsedView", root.transform);
        Band(collapsed.GetComponent<RectTransform>(), 0f, 80f, 0f, 0f);
        BuildHeader(collapsed.transform, category, catColor, title, true, summary);

        // Expanded view (fills the card, header repeated at top).
        var expanded = NewUI("ExpandedView", root.transform);
        Stretch(expanded.GetComponent<RectTransform>());
        BuildHeader(expanded.transform, category, catColor, title, false, null);

        var toggle = root.AddComponent<CardToggle>();
        toggle.collapsedView = collapsed;
        toggle.expandedView = expanded;
        toggle.collapsedHeight = 80f;
        toggle.expandedHeight = height;
        toggle.expanded = false;

        collapsed.SetActive(true);
        expanded.SetActive(false);

        return new CardParts { root = root, rt = rt, collapsed = collapsed, expanded = expanded };
    }

    static void BuildHeader(Transform parent, string category, Color catColor, string title,
        bool withSummary, string summary)
    {
        // Category badge.
        var badge = AddImage(parent, "CategoryBadge", catColor);
        var brt = badge.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 1f);
        brt.sizeDelta = new Vector2(Mathf.Max(64f, category.Length * 8f + 16f), 18f);
        brt.anchoredPosition = new Vector2(10f, -8f);
        var bt = AddText(badge.transform, "Text", category.ToUpperInvariant(), 10, BgColor,
            FontStyle.Bold, TextAnchor.MiddleCenter, false);
        Stretch(bt.rectTransform);

        // Title (14pt, white, bold, max 30 chars).
        var titleT = AddText(parent, "Title", Truncate(title, 30), 14, White, FontStyle.Bold,
            TextAnchor.UpperLeft, false);
        Band(titleT.rectTransform, 30f, 22f, 10f, 10f);

        if (withSummary)
        {
            var sumT = AddText(parent, "Summary", Truncate(summary, 80), 11, Alpha(White, 0.8f),
                FontStyle.Normal, TextAnchor.UpperLeft, true);
            Band(sumT.rectTransform, 54f, 24f, 10f, 10f);
        }
    }

    // =====================================================================
    //  CARD 01 - figure (FIG1) - primary 320x420
    // =====================================================================
    static void BuildCard01(Transform parent, Vector2 pos)
    {
        var card = CreateCard(parent, "Card01_RAG_System", 320f, 420f, pos,
            "contribution", Hex("#FFB800"), "RAG Facial Animation System",
            "LLM+RAG pipeline animates NPC faces via FACS action units automatically.");
        var e = card.expanded.transform;

        var fig = AddImage(e, "Figure", White);
        fig.sprite = LoadSprite("FIG1");
        fig.preserveAspect = true;
        Band(fig.rectTransform, 60f, 184f, 12f, 12f);

        var caption = AddText(e, "Caption",
            "POC system architecture: Plugin → RAG App → OpenFace → Redis → LLM Animator → Game RT App.",
            10, Alpha(White, 0.7f), FontStyle.Italic, TextAnchor.UpperLeft, true);
        Band(caption.rectTransform, 250f, 30f, 12f, 12f);

        var desc = AddText(e, "Description",
            "Diagram showing five components: Modeling App/Game Engine Plugin, RAG App Pod with OpenFace, Redis Cache DB, LLM AI Animator Agent, and Game/RT App exchanging blend shape and animation data.",
            11, White, FontStyle.Normal, TextAnchor.UpperLeft, true);
        Band(desc.rectTransform, 284f, 128f, 12f, 12f);
    }

    // =====================================================================
    //  CARD 02 - animation - secondary 260x360
    // =====================================================================
    static void BuildCard02(Transform parent, Vector2 pos)
    {
        var catColor = Hex("#00D4FF");
        var card = CreateCard(parent, "Card02_Animation_Pipeline", 260f, 360f, pos,
            "method", catColor, "How an Animation Is Born",
            "Four-step pipeline turns a target emotion into a JSON keyframe timeline.");
        var e = card.expanded.transform;

        var label = AddText(e, "FrameLabel", "1. Request", 12, White, FontStyle.Bold,
            TextAnchor.MiddleCenter, true);
        Band(label.rectTransform, 58f, 24f, 10f, 10f);

        // Content container (target for fade/slide).
        var content = NewUI("Content", e);
        Band(content.GetComponent<RectTransform>(), 88f, 168f, 10f, 10f);
        var contentGroup = content.AddComponent<CanvasGroup>();

        var frameImg = AddImage(content.transform, "FrameImage", White);
        frameImg.preserveAspect = true;
        Band(frameImg.rectTransform, 0f, 90f, 8f, 8f);
        frameImg.gameObject.SetActive(false);

        var frameDesc = AddText(content.transform, "FrameDescription", "", 11, White,
            FontStyle.Normal, TextAnchor.UpperCenter, true);
        Band(frameDesc.rectTransform, 4f, 160f, 6f, 6f);

        var counter = AddText(e, "Counter", "1 / 4", 9, Alpha(White, 0.6f), FontStyle.Normal,
            TextAnchor.MiddleCenter, false);
        Band(counter.rectTransform, 300f, 16f, 10f, 10f);

        var prev = MakeButton(e, "PrevButton", "‹ Prev", catColor);
        var prt = prev.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 1f);
        prt.sizeDelta = new Vector2(78f, 30f); prt.anchoredPosition = new Vector2(12f, -320f);

        var next = MakeButton(e, "NextButton", "Next ›", catColor);
        var nrt = next.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(1f, 1f); nrt.anchorMax = new Vector2(1f, 1f); nrt.pivot = new Vector2(1f, 1f);
        nrt.sizeDelta = new Vector2(78f, 30f); nrt.anchoredPosition = new Vector2(-12f, -320f);

        var view = card.expanded.AddComponent<AnimationFrameView>();
        view.labelText = label;
        view.descriptionText = frameDesc;
        view.imageDisplay = frameImg;
        view.counterText = counter;
        view.prevButton = prev.GetComponent<Button>();
        view.nextButton = next.GetComponent<Button>();
        view.contentGroup = contentGroup;
        view.contentRect = content.GetComponent<RectTransform>();
        view.transition = AnimationFrameView.Transition.Slide; // transitionType = "slide"
        view.looping = false;
        view.frames = new[]
        {
            new AnimationFrameView.Frame { label = "1. Request",  description = "Game app sends modelID, confidence threshold, and target emotion (e.g. 'happiness') to RAG App." },
            new AnimationFrameView.Frame { label = "2. Retrieve", description = "RAG App queries Redis for the character's blend shape–AU map, filtered by confidence score." },
            new AnimationFrameView.Frame { label = "3. Generate", description = "LLM Animator receives the filtered map + emotion and produces a JSON animation timeline with keyframes (0–100 activation)." },
            new AnimationFrameView.Frame { label = "4. Apply",    description = "Game runtime receives the JSON and drives blend shape weights on the 3D character in real time." },
        };
    }

    // =====================================================================
    //  CARD 03 - chart (bar) - secondary 260x360
    // =====================================================================
    static void BuildCard03(Transform parent, Vector2 pos)
    {
        var catColor = Hex("#FFFFFF");
        var card = CreateCard(parent, "Card03_Fidelity_Chart", 260f, 360f, pos,
            "result", catColor, "Fidelity by Emotion",
            "Anger scored highest (3.87); contempt lowest (2.29) on a 5-point Likert scale.");
        var e = card.expanded.transform;

        var chartTitle = AddText(e, "ChartTitle", "Mean Animation Fidelity per Emotion (Likert 1–5)",
            11, White, FontStyle.Bold, TextAnchor.UpperCenter, true);
        Band(chartTitle.rectTransform, 58f, 28f, 8f, 8f);

        // Plot frame: 150px plot height sitting on a 22px label strip.
        var frame = NewUI("PlotFrame", e);
        var frameRt = frame.GetComponent<RectTransform>();
        Band(frameRt, 88f, 176f, 10f, 10f);

        const float plotH = 150f;
        const float baseY = 22f;
        const float leftPad = 0.13f;   // fraction reserved for y-axis labels
        const float rightPad = 0.02f;
        const float yMin = 0f, yMax = 5f;

        string[] labels = { "Anger", "Happiness", "Surprise", "Sadness", "Disgust", "Fear", "Contempt" };
        float[] values = { 3.87f, 3.41f, 3.34f, 2.89f, 2.58f, 2.52f, 2.29f };
        int n = values.Length;
        float spanW = 1f - leftPad - rightPad;
        float slot = spanW / n;

        // Y-axis gridlines + labels (0..5).
        for (int v = 0; v <= 5; v++)
        {
            float f = (v - yMin) / (yMax - yMin);
            float y = baseY + f * plotH;

            var yl = AddText(frame.transform, "Y" + v, v.ToString(), 9, Alpha(White, 0.7f),
                FontStyle.Normal, TextAnchor.MiddleRight, false);
            var yr = yl.rectTransform;
            yr.anchorMin = new Vector2(0f, 0f); yr.anchorMax = new Vector2(0f, 0f); yr.pivot = new Vector2(0f, 0.5f);
            yr.sizeDelta = new Vector2(leftPad * 230f, 12f);
            yr.anchoredPosition = new Vector2(0f, y);
        }

        // Bars.
        for (int i = 0; i < n; i++)
        {
            float cx = leftPad + slot * (i + 0.5f);
            float half = slot * 0.36f;
            float h = Mathf.Clamp01((values[i] - yMin) / (yMax - yMin)) * plotH;

            var bar = AddImage(frame.transform, "Bar_" + labels[i], catColor);
            var r = bar.rectTransform;
            r.anchorMin = new Vector2(cx - half, 0f);
            r.anchorMax = new Vector2(cx + half, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(0f, baseY);
            r.offsetMax = new Vector2(0f, baseY + h);

            // Value label above each bar.
            var vl = AddText(frame.transform, "Val_" + i, values[i].ToString("0.00"), 8,
                Alpha(White, 0.85f), FontStyle.Normal, TextAnchor.LowerCenter, false);
            var vr = vl.rectTransform;
            vr.anchorMin = new Vector2(cx - half - 0.02f, 0f);
            vr.anchorMax = new Vector2(cx + half + 0.02f, 0f);
            vr.pivot = new Vector2(0.5f, 0f);
            vr.offsetMin = new Vector2(0f, baseY + h);
            vr.offsetMax = new Vector2(0f, baseY + h + 12f);

            // X-axis label (max 12 chars), below baseline.
            var xl = AddText(frame.transform, "Lbl_" + i, Truncate(labels[i], 12), 9, White,
                FontStyle.Normal, TextAnchor.UpperCenter, true);
            var xr = xl.rectTransform;
            xr.anchorMin = new Vector2(cx - slot * 0.5f, 0f);
            xr.anchorMax = new Vector2(cx + slot * 0.5f, 0f);
            xr.pivot = new Vector2(0.5f, 1f);
            xr.offsetMin = new Vector2(0f, 0f);
            xr.offsetMax = new Vector2(0f, baseY);
        }

        // Reference line: overall mean (2.99) - dashed.
        float refF = (2.99f - yMin) / (yMax - yMin);
        float refY = baseY + refF * plotH;
        BuildDashedLine(frame.transform, leftPad, 1f - rightPad, refY, Alpha(catColor, 0.7f));
        var refLbl = AddText(frame.transform, "RefLabel", "Overall mean (2.99)", 8,
            Alpha(White, 0.75f), FontStyle.Italic, TextAnchor.LowerRight, false);
        var rlr = refLbl.rectTransform;
        rlr.anchorMin = new Vector2(1f - rightPad - 0.45f, 0f);
        rlr.anchorMax = new Vector2(1f - rightPad, 0f);
        rlr.pivot = new Vector2(1f, 0f);
        rlr.offsetMin = new Vector2(0f, refY + 1f);
        rlr.offsetMax = new Vector2(0f, refY + 13f);

        var note = AddText(e, "Note",
            "Lowest per-character: Asuna (2.30). Highest per-character: Ja-Long (3.71).",
            10, Alpha(White, 0.8f), FontStyle.Normal, TextAnchor.UpperLeft, true);
        Band(note.rectTransform, 270f, 84f, 12f, 12f);
    }

    // =====================================================================
    //  CARD 04 - chart (comparison_table) - secondary 260x360
    // =====================================================================
    static void BuildCard04(Transform parent, Vector2 pos)
    {
        var catColor = Hex("#00FF88");
        var card = CreateCard(parent, "Card04_Duration_Table", 260f, 360f, pos,
            "metric", catColor, "Generation Time vs Real-Time",
            "Mean 8.2 s generation time rules out real-time use but fits level loading.");
        var e = card.expanded.transform;

        var title = AddText(e, "TableTitle", "Animation Generation Duration (seconds)",
            11, White, FontStyle.Bold, TextAnchor.UpperCenter, true);
        Band(title.rectTransform, 58f, 28f, 8f, 8f);

        var grid = NewUI("Table", e);
        Band(grid.GetComponent<RectTransform>(), 90f, 180f, 12f, 12f);
        var glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(116f, 28f);
        glg.spacing = new Vector2(2f, 2f);
        glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment = TextAnchor.UpperCenter;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;

        string[] columns = { "Metric", "Value" };
        string[][] rows =
        {
            new[] { "Minimum", "3.4 s" },
            new[] { "Mean", "8.2 s" },
            new[] { "Maximum", "25.3 s" },
            new[] { "Typical range", "3.4 s – 9.4 s" },
            new[] { "Prompt target", "3 s (LLM instruction)" },
        };

        // Header row (bold, 10pt, category-colored background, max 15 chars).
        for (int c = 0; c < 2; c++)
            MakeCell(grid.transform, Truncate(columns[c], 15), catColor, BgColor, 10, FontStyle.Bold, true);

        // Data rows: alternating opacity 100% / 60% (max 20 chars per cell).
        for (int r = 0; r < rows.Length; r++)
        {
            bool full = (r % 2 == 0);
            float bgA = full ? 0.10f : 0.04f;
            float txtA = full ? 1.0f : 0.6f;
            for (int c = 0; c < 2; c++)
                MakeCell(grid.transform, Truncate(rows[r][c], 20), Alpha(White, bgA),
                    Alpha(White, txtA), 9, FontStyle.Normal, false);
        }

        var note = AddText(e, "Note",
            "Distribution is right-skewed: most results cluster near the low end. Authors conclude the system is viable during level loading phases, not for instant in-gameplay generation.",
            10, Alpha(White, 0.8f), FontStyle.Normal, TextAnchor.UpperLeft, true);
        Band(note.rectTransform, 278f, 78f, 12f, 12f);
    }

    static void MakeCell(Transform parent, string text, Color bgColor, Color textColor,
        int fontSize, FontStyle style, bool center)
    {
        var cell = AddImage(parent, "Cell", bgColor);
        var t = AddText(cell.transform, "Text", text, fontSize, textColor, style,
            center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, false);
        var r = t.rectTransform;
        Stretch(r);
        r.offsetMin = new Vector2(6f, 0f);
        r.offsetMax = new Vector2(-4f, 0f);
    }

    // =====================================================================
    //  CARD 05 - figure (FIG4) - secondary 260x360
    // =====================================================================
    static void BuildCard05(Transform parent, Vector2 pos)
    {
        var card = CreateCard(parent, "Card05_Generated_Expressions", 260f, 360f, pos,
            "result", Hex("#FFFFFF"), "Generated Expressions",
            "Seven characters, seven emotions — system output shown across diverse 3D models.");
        var e = card.expanded.transform;

        var fig = AddImage(e, "Figure", White);
        fig.sprite = LoadSprite("FIG4");
        fig.preserveAspect = true;
        Band(fig.rectTransform, 58f, 120f, 12f, 12f);

        var caption = AddText(e, "Caption",
            "L→R: Alina (happiness), Asuna (anger), Atticus (sadness), Disa (fear), Ja-Long (contempt), Zaniyah (disgust), Khalan (surprise).",
            10, Alpha(White, 0.7f), FontStyle.Italic, TextAnchor.UpperLeft, true);
        Band(caption.rectTransform, 184f, 44f, 12f, 12f);

        var desc = AddText(e, "Description",
            "A row of seven 3D character head renders each displaying a distinct facial expression corresponding to one of the seven FACS-defined test emotions, demonstrating visual output quality and variation across character models.",
            11, White, FontStyle.Normal, TextAnchor.UpperLeft, true);
        Band(desc.rectTransform, 232f, 120f, 12f, 12f);
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image AddImage(Transform parent, string name, Color color)
    {
        var go = NewUI(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static Text AddText(Transform parent, string name, string content, int fontSize, Color color,
        FontStyle style, TextAnchor align, bool wrap)
    {
        var go = NewUI(name, parent);
        var t = go.AddComponent<Text>();
        t.font = GetFont();
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Color catColor)
    {
        var img = AddImage(parent, name, Alpha(catColor, 0.22f));
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var t = AddText(img.transform, "Text", label, 11, catColor, FontStyle.Bold,
            TextAnchor.MiddleCenter, false);
        Stretch(t.rectTransform);
        return img.gameObject;
    }

    static void BuildDashedLine(Transform parent, float xFromFrac, float xToFrac, float y, Color color)
    {
        const int dashes = 14;
        float total = xToFrac - xFromFrac;
        float step = total / dashes;
        for (int i = 0; i < dashes; i += 1)
        {
            float a = xFromFrac + step * i;
            float b = a + step * 0.55f;
            var seg = AddImage(parent, "Dash_" + i, color);
            var r = seg.rectTransform;
            r.anchorMin = new Vector2(a, 0f);
            r.anchorMax = new Vector2(b, 0f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(0f, y - 1f);
            r.offsetMax = new Vector2(0f, y + 1f);
        }
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Horizontal band anchored to the top of the parent.
    static void Band(RectTransform rt, float top, float height, float left, float right)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMax = new Vector2(-right, -top);
        rt.offsetMin = new Vector2(left, -(top + height));
    }

    static Sprite LoadSprite(string assetRef)
    {
        string file = assetRef.Replace("FIG", "FIG_"); // FIG1 -> FIG_1
        string path = FigureFolder + file + ".png";
        EnsureSprite(path);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogWarning("PaperCave: could not load sprite at " + path);
        return sprite;
    }

    static void EnsureSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, Mathf.Max(0, max - 1)).TrimEnd() + "…";
    }

    static Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }

    static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }

    static Color Alpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
