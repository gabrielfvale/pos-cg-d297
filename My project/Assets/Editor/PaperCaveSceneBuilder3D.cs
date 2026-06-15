using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using PaperCave;

/// <summary>
/// Builds the "PaperCave_Cards_3D" scene: five interactive 3D card objects in
/// world space, generated from the Card Manifest. Each card is a Quad face with
/// emissive borders, a World Space Canvas (TextMeshPro) for collapsed/expanded
/// content, a thin box collider + kinematic Rigidbody for click/drag, and floats
/// forward + scales up when clicked. Runtime behaviour lives in
/// Assets/Scripts/PaperCave/ (Card3D, Card3DController, Card3DButton,
/// AnimationFrameView3D).
///
/// Run via menu: Tools > PaperCave > Build PaperCave_Cards_3D Scene.
/// </summary>
public static class PaperCaveSceneBuilder3D
{
    // ---- Palette ---------------------------------------------------------
    static readonly Color BgColor = Hex("#0A1628");
    static readonly Color White   = Color.white;

    const string FigureFolder = "Assets/Paper Figures/";
    const string MatFolder    = "Assets/PaperCave3D/Materials/";
    const string ScenePath    = "Assets/Scenes/PaperCave_Cards_3D.unity";

    static TMP_FontAsset _font;

    [MenuItem("Tools/PaperCave/Build PaperCave_Cards_3D Scene")]
    public static void Build()
    {
        EnsureFolders();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- Camera ------------------------------------------------------
        var camGO = new GameObject("Main Camera", typeof(Camera));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.transform.position = new Vector3(0f, 1.6f, -6f);
        cam.transform.rotation = Quaternion.LookRotation((Vector3.zero - cam.transform.position).normalized, Vector3.up);
        camGO.AddComponent<AudioListener>();

        // ---- Lights ------------------------------------------------------
        // "Ambient" directional fill in a deep blue.
        var dirGO = new GameObject("Directional Light", typeof(Light));
        var dir = dirGO.GetComponent<Light>();
        dir.type = LightType.Directional;
        dir.color = Hex("#1A2A4A");
        dir.intensity = 0.4f;
        dirGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // White point light above the arc.
        var pointGO = new GameObject("Point Light", typeof(Light));
        var point = pointGO.GetComponent<Light>();
        point.type = LightType.Point;
        point.color = White;
        point.intensity = 1.0f;
        point.range = 12f;
        pointGO.transform.position = new Vector3(0f, 3f, 0f);

        // ---- Input controller -------------------------------------------
        var ctrlGO = new GameObject("CardController");
        var ctrl = ctrlGO.AddComponent<Card3DController>();
        ctrl.targetCamera = cam;

        // ---- Paper title (non-interactive context label) -----------------
        var titleGO = new GameObject("PaperTitle");
        var titleTMP = titleGO.AddComponent<TextMeshPro>();
        titleTMP.font = TMPFont();
        titleTMP.text = "Generative AI for Facial Expressions in 3D Game Characters\n<size=70%>A Retrieval-Augmented Approach</size>";
        titleTMP.fontSize = 1.0f;
        titleTMP.color = Alpha(White, 0.85f);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleGO.GetComponent<RectTransform>().sizeDelta = new Vector2(10f, 2f);
        titleGO.transform.position = new Vector3(0f, 2.2f, 0.5f);

        // ---- Cards (arc facing the camera) -------------------------------
        BuildCard01(new Vector3( 0.0f,  0.0f, 0.0f),   0f);
        BuildCard02(new Vector3(-2.0f, -0.3f, 0.2f),  12f);
        BuildCard03(new Vector3( 2.0f, -0.3f, 0.2f), -12f);
        BuildCard04(new Vector3(-1.0f, -1.8f, 0.3f),   7f);
        BuildCard05(new Vector3( 1.0f, -1.8f, 0.3f),  -7f);

        // ---- Save --------------------------------------------------------
        string absDir = System.IO.Path.GetDirectoryName(
            System.IO.Path.Combine(Application.dataPath, "../" + ScenePath));
        System.IO.Directory.CreateDirectory(absDir);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("PaperCave3D: scene built and saved to " + ScenePath);
    }

    // =====================================================================
    //  CARD SHELL
    // =====================================================================

    class CardParts
    {
        public GameObject root;
        public Card3D card;
        public Transform collapsed;
        public Transform expanded;
        public float pxW, pxH;   // canvas pixel size
        public Color catColor;
    }

    static CardParts CreateCard(string name, float w, float h, Vector3 pos, float yRot,
        string category, Color catColor, string title, string summary)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(w, h, 0.04f);
        box.center = Vector3.zero;

        var card = root.AddComponent<Card3D>();

        // Card face (Quad faces -Z, toward the camera).
        MakeQuad("Face", root.transform, new Vector3(0f, 0f, 0f),
            new Vector3(w, h, 1f), Quaternion.identity, FaceMat());

        // Emissive border: 4 thin elongated quads, just in front of the face.
        var emis = EmissiveMat(catColor);
        const float t = 0.05f;
        const float bz = -0.004f;
        MakeQuad("Border_Top",    root.transform, new Vector3(0f,  h * 0.5f, bz), new Vector3(w + t, t, 1f), Quaternion.identity, emis);
        MakeQuad("Border_Bottom", root.transform, new Vector3(0f, -h * 0.5f, bz), new Vector3(w + t, t, 1f), Quaternion.identity, emis);
        MakeQuad("Border_Left",   root.transform, new Vector3(-w * 0.5f, 0f, bz), new Vector3(t, h + t, 1f), Quaternion.identity, emis);
        MakeQuad("Border_Right",  root.transform, new Vector3( w * 0.5f, 0f, bz), new Vector3(t, h + t, 1f), Quaternion.identity, emis);

        // World Space Canvas (designed in "px" then scaled by 0.01 to world).
        float pxW = w * 100f, pxH = h * 100f;
        var canvasGO = new GameObject("Canvas", typeof(Canvas));
        canvasGO.transform.SetParent(root.transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var crt = (RectTransform)canvasGO.transform;
        crt.sizeDelta = new Vector2(pxW, pxH);
        crt.localPosition = new Vector3(0f, 0f, -0.012f); // in front of the face
        crt.localRotation = Quaternion.identity;
        crt.localScale = Vector3.one * 0.01f;

        var collapsed = NewUI("Collapsed", canvasGO.transform);
        Stretch(collapsed.GetComponent<RectTransform>());
        var expanded = NewUI("Expanded", canvasGO.transform);
        Stretch(expanded.GetComponent<RectTransform>());

        BuildCollapsed(collapsed.transform, category, catColor, title, summary);

        card.collapsedView = collapsed;
        card.expandedView = expanded;
        collapsed.SetActive(true);
        expanded.SetActive(false);

        return new CardParts
        {
            root = root, card = card, collapsed = collapsed.transform,
            expanded = expanded.transform, pxW = pxW, pxH = pxH, catColor = catColor
        };
    }

    // Collapsed face: category badge + title + summary.
    static void BuildCollapsed(Transform parent, string category, Color catColor,
        string title, string summary)
    {
        BuildBadge(parent, category, catColor, 8f);

        var titleT = AddTMP(parent, "Title", Truncate(title, 30), 12f, White, true, false,
            TextAlignmentOptions.TopLeft, true);
        Band(titleT.rectTransform, 30f, 36f, 8f, 8f);

        var sumT = AddTMP(parent, "Summary", Truncate(summary, 80), 9f, Alpha(White, 0.8f),
            false, false, TextAlignmentOptions.TopLeft, true);
        Band(sumT.rectTransform, 70f, 48f, 8f, 8f);
    }

    // Compact header reused by every expanded view (badge + title strip).
    static void BuildExpandedHeader(Transform parent, string category, Color catColor, string title)
    {
        BuildBadge(parent, category, catColor, 6f);
        var titleT = AddTMP(parent, "Title", Truncate(title, 30), 11f, White, true, false,
            TextAlignmentOptions.TopLeft, false);
        // Auto-shrink so the full title always fits the narrow expanded header.
        titleT.enableAutoSizing = true;
        titleT.fontSizeMin = 6f;
        titleT.fontSizeMax = 11f;
        Band(titleT.rectTransform, 24f, 22f, 8f, 8f);
    }

    static void BuildBadge(Transform parent, string category, Color catColor, float top)
    {
        string label = category.ToUpperInvariant();
        var badge = AddImage(parent, "CategoryBadge", catColor);
        var brt = badge.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 1f);
        brt.sizeDelta = new Vector2(Mathf.Max(40f, label.Length * 6f + 10f), 14f);
        brt.anchoredPosition = new Vector2(8f, -top);
        var bt = AddTMP(badge.transform, "Text", label, 8f, BgColor, true, false,
            TextAlignmentOptions.Center, false);
        Stretch(bt.rectTransform);
    }

    // =====================================================================
    //  CARD 01 - figure (FIG1) - primary, golden amber
    // =====================================================================
    static void BuildCard01(Vector3 pos, float yRot)
    {
        var catColor = Hex("#FFB800");
        var c = CreateCard("Card01_RAG_System", 1.6f, 2.2f, pos, yRot,
            "contribution", catColor, "RAG Facial Animation System",
            "LLM+RAG pipeline animates NPC faces via FACS action units automatically.");
        var e = c.expanded;
        BuildExpandedHeader(e, "contribution", catColor, "RAG Facial Animation System");

        AddFigure(e, "Figure", LoadTex("FIG1"), 50f, 92f, 12f, 12f);

        var caption = AddTMP(e, "Caption",
            "POC system architecture: Plugin → RAG App → OpenFace → Redis → LLM Animator → Game RT App.",
            8f, Alpha(White, 0.7f), false, true, TextAlignmentOptions.TopLeft, true);
        Band(caption.rectTransform, 146f, 26f, 12f, 12f);

        var desc = AddTMP(e, "Description",
            "Five components — Modeling/Game-Engine Plugin, RAG App Pod with OpenFace, Redis cache, LLM Animator agent, and Game/RT App — exchange blend shape and animation data.",
            9f, White, false, false, TextAlignmentOptions.TopLeft, true);
        desc.maxVisibleLines = 4;
        Band(desc.rectTransform, 174f, 40f, 12f, 12f);

        // Golden point light that picks out the primary card in 3D space.
        var lightGO = new GameObject("PrimaryHighlight", typeof(Light));
        lightGO.transform.SetParent(c.root.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0f, -0.4f);
        var l = lightGO.GetComponent<Light>();
        l.type = LightType.Point;
        l.color = catColor;
        l.intensity = 0.8f;
        l.range = 2.0f;
    }

    // =====================================================================
    //  CARD 02 - animation - secondary, cyan blue
    // =====================================================================
    static void BuildCard02(Vector3 pos, float yRot)
    {
        var catColor = Hex("#00D4FF");
        var c = CreateCard("Card02_Animation_Pipeline", 1.3f, 1.8f, pos, yRot,
            "method", catColor, "How an Animation Is Born",
            "Four-step pipeline turns a target emotion into a JSON keyframe timeline.");
        var e = c.expanded;
        BuildExpandedHeader(e, "method", catColor, "How an Animation Is Born");

        var label = AddTMP(e, "FrameLabel", "1. Request", 10f, White, true, false,
            TextAlignmentOptions.Center, true);
        Band(label.rectTransform, 44f, 16f, 6f, 6f);

        // Sliding content container.
        var content = NewUI("Content", e);
        Band(content.GetComponent<RectTransform>(), 62f, 68f, 8f, 8f);
        var contentGroup = content.AddComponent<CanvasGroup>();

        var frameImg = NewUI("FrameImage", content.transform);
        var raw = frameImg.AddComponent<RawImage>();
        raw.raycastTarget = false;
        Stretch(raw.rectTransform);
        frameImg.SetActive(false);

        var frameDesc = AddTMP(content.transform, "FrameDescription", "", 9f, White,
            false, false, TextAlignmentOptions.Top, true);
        Stretch(frameDesc.rectTransform);

        var counter = AddTMP(e, "Counter", "1 / 4", 7f, Alpha(White, 0.6f), false, false,
            TextAlignmentOptions.Center, false);
        Band(counter.rectTransform, 134f, 14f, 6f, 6f);

        // Frame view component.
        var view = c.expanded.gameObject.AddComponent<AnimationFrameView3D>();
        view.labelText = label;
        view.descriptionText = frameDesc;
        view.imageDisplay = raw;
        view.counterText = counter;
        view.contentGroup = contentGroup;
        view.contentRect = content.GetComponent<RectTransform>();
        view.transition = AnimationFrameView3D.Transition.Slide; // transitionType = "slide"
        view.looping = false;
        view.frames = new[]
        {
            new AnimationFrameView3D.Frame { label = "1. Request",  description = "Game app sends modelID, confidence threshold, and target emotion (e.g. 'happiness') to RAG App." },
            new AnimationFrameView3D.Frame { label = "2. Retrieve", description = "RAG App queries Redis for the character's blend shape–AU map, filtered by confidence score." },
            new AnimationFrameView3D.Frame { label = "3. Generate", description = "LLM Animator receives the filtered map + emotion and produces a JSON animation timeline with keyframes (0–100 activation)." },
            new AnimationFrameView3D.Frame { label = "4. Apply",    description = "Game runtime receives the JSON and drives blend shape weights on the 3D character in real time." },
        };

        // Two physical 3D button planes at the card bottom (toggled with expand).
        var buttons = new GameObject("AnimButtons");
        buttons.transform.SetParent(c.root.transform, false);
        buttons.SetActive(false);
        c.card.expandedExtra = buttons;

        MakeAnimButton(buttons.transform, "PrevButton", "‹ PREV", catColor, view, -1,
            new Vector3(-0.33f, -0.74f, -0.03f));
        MakeAnimButton(buttons.transform, "NextButton", "NEXT ›", catColor, view, +1,
            new Vector3( 0.33f, -0.74f, -0.03f));
    }

    static void MakeAnimButton(Transform parent, string name, string text, Color color,
        AnimationFrameView3D view, int dir, Vector3 localPos)
    {
        var quad = MakeQuad(name, parent, localPos, new Vector3(0.52f, 0.18f, 1f),
            Quaternion.identity, EmissiveMat(color));
        var col = quad.AddComponent<BoxCollider>();
        col.size = new Vector3(1f, 1f, 0.2f); // local; scaled by the quad's transform
        var btn = quad.AddComponent<Card3DButton>();
        btn.target = view;
        btn.direction = dir;

        // Crisp 3D label sitting just in front of the button plane.
        var labelGO = new GameObject(name + "_Label");
        labelGO.transform.SetParent(parent, false);
        labelGO.transform.localPosition = localPos + new Vector3(0f, 0f, -0.02f);
        var tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.font = TMPFont();
        tmp.text = text;
        tmp.fontSize = 1.1f;
        tmp.color = BgColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        ((RectTransform)labelGO.transform).sizeDelta = new Vector2(0.5f, 0.18f);
    }

    // =====================================================================
    //  CARD 03 - chart (bar) - secondary, bright white
    // =====================================================================
    static void BuildCard03(Vector3 pos, float yRot)
    {
        var catColor = Hex("#FFFFFF");
        var c = CreateCard("Card03_Fidelity_Chart", 1.3f, 1.8f, pos, yRot,
            "result", catColor, "Fidelity by Emotion",
            "Anger scored highest (3.87); contempt lowest (2.29) on a 5-point Likert scale.");
        var e = c.expanded;
        BuildExpandedHeader(e, "result", catColor, "Fidelity by Emotion");

        var chartTitle = AddTMP(e, "ChartTitle", "Mean Animation Fidelity per Emotion (Likert 1–5)",
            8f, White, true, false, TextAlignmentOptions.Top, true);
        Band(chartTitle.rectTransform, 42f, 18f, 8f, 8f);

        // Plot frame: bars sit on a label strip.
        var frame = NewUI("PlotFrame", e);
        Band(frame.GetComponent<RectTransform>(), 62f, 92f, 8f, 8f);

        const float plotH = 56f;
        const float baseY = 16f;
        const float leftPad = 0.04f;
        const float rightPad = 0.02f;
        const float yMin = 0f, yMax = 5f;

        string[] labels = { "Anger", "Happiness", "Surprise", "Sadness", "Disgust", "Fear", "Contempt" };
        float[] values = { 3.87f, 3.41f, 3.34f, 2.89f, 2.58f, 2.52f, 2.29f };
        int n = values.Length;
        float spanW = 1f - leftPad - rightPad;
        float slot = spanW / n;

        for (int i = 0; i < n; i++)
        {
            float cx = leftPad + slot * (i + 0.5f);
            float half = slot * 0.34f;
            float hgt = Mathf.Clamp01((values[i] - yMin) / (yMax - yMin)) * plotH;

            var bar = AddImage(frame.transform, "Bar_" + labels[i], catColor);
            var r = bar.rectTransform;
            r.anchorMin = new Vector2(cx - half, 0f);
            r.anchorMax = new Vector2(cx + half, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(0f, baseY);
            r.offsetMax = new Vector2(0f, baseY + hgt);

            var vl = AddTMP(frame.transform, "Val_" + i, values[i].ToString("0.0"), 6f,
                Alpha(White, 0.85f), false, false, TextAlignmentOptions.Bottom, false);
            var vr = vl.rectTransform;
            vr.anchorMin = new Vector2(cx - slot * 0.5f, 0f);
            vr.anchorMax = new Vector2(cx + slot * 0.5f, 0f);
            vr.pivot = new Vector2(0.5f, 0f);
            vr.offsetMin = new Vector2(0f, baseY + hgt);
            vr.offsetMax = new Vector2(0f, baseY + hgt + 12f);

            var xl = AddTMP(frame.transform, "Lbl_" + i, Truncate(labels[i], 12), 6f, White,
                false, false, TextAlignmentOptions.Top, true);
            var xr = xl.rectTransform;
            xr.anchorMin = new Vector2(cx - slot * 0.5f, 0f);
            xr.anchorMax = new Vector2(cx + slot * 0.5f, 0f);
            xr.pivot = new Vector2(0.5f, 1f);
            xr.offsetMin = new Vector2(0f, 0f);
            xr.offsetMax = new Vector2(0f, baseY);
        }

        // Threshold / reference line: overall mean (2.99), white semi-transparent.
        float refY = baseY + Mathf.Clamp01((2.99f - yMin) / (yMax - yMin)) * plotH;
        var refLine = AddImage(frame.transform, "RefLine", Alpha(White, 0.5f));
        var rr = refLine.rectTransform;
        rr.anchorMin = new Vector2(leftPad, 0f);
        rr.anchorMax = new Vector2(1f - rightPad, 0f);
        rr.pivot = new Vector2(0.5f, 0.5f);
        rr.offsetMin = new Vector2(0f, refY - 0.5f);
        rr.offsetMax = new Vector2(0f, refY + 0.5f);

        var refLbl = AddTMP(frame.transform, "RefLabel", "mean 2.99", 6f, Alpha(White, 0.7f),
            false, true, TextAlignmentOptions.BottomRight, false);
        var rl = refLbl.rectTransform;
        rl.anchorMin = new Vector2(1f - rightPad - 0.4f, 0f);
        rl.anchorMax = new Vector2(1f - rightPad, 0f);
        rl.pivot = new Vector2(1f, 0f);
        rl.offsetMin = new Vector2(0f, refY + 1f);
        rl.offsetMax = new Vector2(0f, refY + 11f);

        var note = AddTMP(e, "Note",
            "Lowest per-character: Asuna (2.30). Highest: Ja-Long (3.71).",
            8f, Alpha(White, 0.8f), false, false, TextAlignmentOptions.TopLeft, true);
        note.maxVisibleLines = 3;
        Band(note.rectTransform, 156f, 24f, 10f, 10f);
    }

    // =====================================================================
    //  CARD 04 - chart (comparison_table) - secondary, neon green
    // =====================================================================
    static void BuildCard04(Vector3 pos, float yRot)
    {
        var catColor = Hex("#00FF88");
        var c = CreateCard("Card04_Duration_Table", 1.3f, 1.8f, pos, yRot,
            "metric", catColor, "Generation Time vs Real-Time",
            "Mean 8.2 s generation time rules out real-time use but fits level loading.");
        var e = c.expanded;
        BuildExpandedHeader(e, "metric", catColor, "Generation Time vs Real-Time");

        var tableTitle = AddTMP(e, "TableTitle", "Animation Generation Duration (seconds)",
            8f, White, true, false, TextAlignmentOptions.Top, true);
        Band(tableTitle.rectTransform, 42f, 16f, 8f, 8f);

        string hex = "#" + ColorUtility.ToHtmlStringRGB(catColor);
        string[] cols = { Truncate("Metric", 15), Truncate("Value", 15) };
        string[][] rows =
        {
            new[] { "Minimum",       "3.4 s" },
            new[] { "Mean",          "8.2 s" },
            new[] { "Maximum",       "25.3 s" },
            new[] { "Typical range", "3.4 s – 9.4 s" },
            new[] { "Prompt target", "3 s (LLM)" },
        };

        var sb = new System.Text.StringBuilder();
        sb.Append($"<b><color={hex}>{cols[0]}<pos=58%>{cols[1]}</color></b>");
        foreach (var row in rows)
            sb.Append($"\n{Truncate(row[0], 20)}<pos=58%>{Truncate(row[1], 20)}");

        var table = AddTMP(e, "Table", sb.ToString(), 9f, White, false, false,
            TextAlignmentOptions.TopLeft, false);
        Band(table.rectTransform, 60f, 74f, 10f, 10f);

        var note = AddTMP(e, "Note",
            "Right-skewed: most results cluster low. Viable during level loading, not instant in-gameplay generation.",
            8f, Alpha(White, 0.8f), false, false, TextAlignmentOptions.TopLeft, true);
        note.maxVisibleLines = 3;
        Band(note.rectTransform, 138f, 42f, 10f, 10f);
    }

    // =====================================================================
    //  CARD 05 - figure (FIG4) - secondary, bright white
    // =====================================================================
    static void BuildCard05(Vector3 pos, float yRot)
    {
        var catColor = Hex("#FFFFFF");
        var c = CreateCard("Card05_Generated_Expressions", 1.3f, 1.8f, pos, yRot,
            "result", catColor, "Generated Expressions",
            "Seven characters, seven emotions — system output shown across diverse 3D models.");
        var e = c.expanded;
        BuildExpandedHeader(e, "result", catColor, "Generated Expressions");

        AddFigure(e, "Figure", LoadTex("FIG4"), 46f, 52f, 8f, 8f);

        var caption = AddTMP(e, "Caption",
            "L→R: Alina, Asuna, Atticus, Disa, Ja-Long, Zaniyah, Khalan (happiness→surprise).",
            8f, Alpha(White, 0.7f), false, true, TextAlignmentOptions.TopLeft, true);
        Band(caption.rectTransform, 100f, 30f, 8f, 8f);

        var desc = AddTMP(e, "Description",
            "Seven 3D character heads each display a distinct FACS test emotion, showing visual output quality and variation across character models.",
            9f, White, false, false, TextAlignmentOptions.TopLeft, true);
        desc.maxVisibleLines = 4;
        Band(desc.rectTransform, 132f, 44f, 8f, 8f);
    }

    // =====================================================================
    //  GEOMETRY + UI HELPERS
    // =====================================================================

    static GameObject MakeQuad(string name, Transform parent, Vector3 localPos,
        Vector3 localScale, Quaternion localRot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

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
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI AddTMP(Transform parent, string name, string content, float fontSize,
        Color color, bool bold, bool italic, TextAlignmentOptions align, bool wrap)
    {
        var go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = TMPFont();
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        FontStyles style = FontStyles.Normal;
        if (bold) style |= FontStyles.Bold;
        if (italic) style |= FontStyles.Italic;
        t.fontStyle = style;
        t.alignment = align;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        t.raycastTarget = false;
        return t;
    }

    // RawImage that keeps the texture's aspect ratio inside a top-anchored band.
    static RawImage AddFigure(Transform parent, string name, Texture tex,
        float top, float height, float left, float right)
    {
        var box = NewUI(name + "_Box", parent);
        Band(box.GetComponent<RectTransform>(), top, height, left, right);

        var imgGO = NewUI(name, box.transform);
        var ri = imgGO.AddComponent<RawImage>();
        ri.texture = tex;
        ri.raycastTarget = false;
        var fitter = imgGO.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (tex != null && tex.height > 0) ? (float)tex.width / tex.height : 1.6f;
        Stretch(imgGO.GetComponent<RectTransform>());
        return ri;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Horizontal band anchored to the top of the parent (offsets in px).
    static void Band(RectTransform rt, float top, float height, float left, float right)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMax = new Vector2(-right, -top);
        rt.offsetMin = new Vector2(left, -(top + height));
    }

    // =====================================================================
    //  MATERIALS / ASSETS
    // =====================================================================

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/PaperCave3D"))
            AssetDatabase.CreateFolder("Assets", "PaperCave3D");
        if (!AssetDatabase.IsValidFolder("Assets/PaperCave3D/Materials"))
            AssetDatabase.CreateFolder("Assets/PaperCave3D", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
    }

    static Shader LitShader() { return Shader.Find("Universal Render Pipeline/Lit"); }

    static Material FaceMat()
    {
        return GetOrCreateMat("CardFace", () =>
        {
            var m = new Material(LitShader());
            m.SetColor("_BaseColor", BgColor);
            m.SetFloat("_Smoothness", 0.1f);
            return m;
        });
    }

    static Material EmissiveMat(Color c)
    {
        string hex = ColorUtility.ToHtmlStringRGB(c);
        return GetOrCreateMat("Emissive_" + hex, () =>
        {
            var m = new Material(LitShader());
            m.SetColor("_BaseColor", Color.black);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b, 1f) * 1.5f);
            return m;
        });
    }

    static Material GetOrCreateMat(string name, System.Func<Material> factory)
    {
        string path = MatFolder + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var m = factory();
        m.name = name;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Texture2D LoadTex(string assetRef)
    {
        string file = assetRef.Replace("FIG", "FIG_"); // FIG1 -> FIG_1
        string path = FigureFolder + file + ".png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) Debug.LogWarning("PaperCave3D: could not load texture at " + path);
        return tex;
    }

    // =====================================================================
    //  MISC
    // =====================================================================

    static TMP_FontAsset TMPFont()
    {
        if (_font != null) return _font;
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (_font == null) _font = TMP_Settings.defaultFontAsset;
        return _font;
    }

    static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, Mathf.Max(0, max - 1)).TrimEnd() + "…";
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    static Color Alpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
