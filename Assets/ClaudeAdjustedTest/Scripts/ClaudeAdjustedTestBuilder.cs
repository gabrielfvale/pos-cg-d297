using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds all five exhibit objects and wires them up programmatically.
/// Attach to a SceneBootstrap GameObject in ClaudeAdjustedTest scene and press Play.
/// </summary>
[ExecuteAlways]
public class ClaudeAdjustedTestBuilder : MonoBehaviour
{
    // Exhibits are spaced 14 units apart on X axis
    const float Spacing = 14f;

    void Awake()
    {
        // only build if not already built
        if (transform.childCount > 0) return;
        BuildScene();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ENTRY POINT
    // ─────────────────────────────────────────────────────────────────────────
    void BuildScene()
    {
        var exhibitRoots = new List<Transform>();

        exhibitRoots.Add(BuildPipelineTerminal(new Vector3(0, 0, 0)));
        exhibitRoots.Add(BuildFaceCalibrationPod(new Vector3(Spacing, 0, 0)));
        exhibitRoots.Add(BuildEmotionFidelityDisplay(new Vector3(Spacing * 2, 0, 0)));
        exhibitRoots.Add(BuildFACSCodex(new Vector3(Spacing * 3, 0, 0)));
        exhibitRoots.Add(BuildLimitationShard(new Vector3(Spacing * 4, 0, 0)));

        SetupCamera(exhibitRoots);
        SetupLighting();
        SetupFloor(exhibitRoots.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  1. PIPELINE TERMINAL
    // ─────────────────────────────────────────────────────────────────────────
    Transform BuildPipelineTerminal(Vector3 origin)
    {
        var root = CreateRoot("PipelineTerminal_Exhibit", origin);
        var pt   = root.gameObject.AddComponent<PipelineTerminal>();

        Color amber  = new Color(1f, 0.72f, 0f);

        // — Housing —
        var housing = CreateCylinder("Housing", root, new Vector3(0, 0.5f, 0),
            new Vector3(1.8f, 1f, 1.8f), amber * 0.25f, true);

        // Curved screen backing (scaled cube)
        var screen = CreateCube("Screen", root, new Vector3(0, 2.2f, -0.05f),
            new Vector3(4.5f, 2.8f, 0.08f), new Color(0.1f, 0.07f, 0f), false);
        AddEmission(screen.GetComponent<Renderer>(), new Color(0.15f, 0.1f, 0f));

        // Screen frame border
        var frame = CreateCube("ScreenFrame", root, new Vector3(0, 2.2f, -0.06f),
            new Vector3(4.65f, 2.95f, 0.04f), amber * 0.6f, true);

        // Scanlines visual (many thin quads)
        for (int sl = 0; sl < 12; sl++)
        {
            float y = 1f + sl * 0.18f;
            var line = CreateCube($"Scanline_{sl}", screen.transform,
                new Vector3(0, -1.1f + sl * 0.18f, 0.06f), new Vector3(1f, 0.005f, 0.01f),
                amber * (sl % 2 == 0 ? 0.08f : 0.04f), false);
        }

        // — Pipeline nodes —
        string[] nodeNames = { "Unity Plugin", "RAG App", "OpenFace", "Redis DB", "LLM Animator" };
        var nodes = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            float x = -1.6f + i * 0.8f;
            float y  = 0f;
            // alternate row for clarity
            if (i == 1 || i == 3) y = 0.35f;
            var node = CreateCube($"Node_{nodeNames[i]}", screen.transform,
                new Vector3(x, y, 0.07f), new Vector3(0.28f, 0.16f, 0.04f),
                amber * 0.8f, true);
            node.AddComponent<BoxCollider>();

            var lbl = CreateTextMesh($"NodeLabel_{i}", node.transform,
                new Vector3(0, 0, -0.6f), 6, amber, nodeNames[i]);

            nodes[i] = node;
        }

        // — Arrows between nodes —
        var arrows = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = nodes[i].transform.localPosition;
            Vector3 b = nodes[i + 1].transform.localPosition;
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b) * 0.5f;
            var arr = CreateCube($"Arrow_{i}", screen.transform,
                new Vector3(mid.x, mid.y, 0.07f), new Vector3(len, 0.03f, 0.02f),
                amber * 0.5f, true);
            arrows[i] = arr;
        }

        // — Tooltip label —
        var tooltip = CreateTextMesh("TooltipLabel", root,
            new Vector3(0, 4.0f, 0), 8, Color.white, "");
        tooltip.anchor = TextAnchor.MiddleCenter;
        tooltip.gameObject.SetActive(false);

        // Wire PipelineTerminal component
        pt.pipelineNodes = nodes;
        pt.arrowObjects  = arrows;
        pt.tooltipLabel  = tooltip;

        // Exhibit label
        CreateExhibitLabel(root, "Pipeline Terminal", "RAG System Architecture — Figure 1", amber);

        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  2. FACE CALIBRATION POD
    // ─────────────────────────────────────────────────────────────────────────
    Transform BuildFaceCalibrationPod(Vector3 origin)
    {
        var root = CreateRoot("FaceCalibrationPod_Exhibit", origin);
        var fcp  = root.gameObject.AddComponent<FaceCalibrationPod>();

        Color cyan    = new Color(0f, 0.83f, 1f);
        Color cyanDim = new Color(0f, 0.35f, 0.5f);

        // Pedestal
        var pedBase  = CreateCylinder("PedestalBase", root, new Vector3(0, 0.1f, 0),
            new Vector3(2f, 0.2f, 2f), cyan * 0.3f, true);
        var pedMid   = CreateCylinder("PedestalMid", root, new Vector3(0, 0.7f, 0),
            new Vector3(1.0f, 1.0f, 1.0f), cyan * 0.2f, false);
        var pedTop   = CreateCylinder("PedestalTop", root, new Vector3(0, 1.25f, 0),
            new Vector3(1.6f, 0.1f, 1.6f), cyan * 0.5f, true);

        // Emissive ring on top
        var ring = CreateTorus("PedestalRing", root, new Vector3(0, 1.3f, 0), cyan);

        // Holographic head (sphere + smaller sphere for jaw)
        var headPivot = new GameObject("HeadPivot").transform;
        headPivot.SetParent(root);
        headPivot.localPosition = new Vector3(0, 2.8f, 0);

        var skull = CreateSphere("Skull", headPivot, new Vector3(0, 0, 0),
            new Vector3(0.7f, 0.8f, 0.7f), new Color(0f, 0.7f, 0.9f, 0.6f), true);
        var jaw   = CreateSphere("Jaw", headPivot, new Vector3(0, -0.38f, 0.05f),
            new Vector3(0.45f, 0.3f, 0.45f), new Color(0f, 0.5f, 0.7f, 0.5f), true);
        var noseB = CreateSphere("Nose", headPivot, new Vector3(0, -0.05f, 0.36f),
            new Vector3(0.12f, 0.1f, 0.12f), cyan * 0.5f, true);

        // Wireframe overlay (slightly larger, wireframe material emulated via thin planes)
        for (int wi = 0; wi < 3; wi++)
        {
            float angle = wi * 60f;
            var wline = CreateCube($"WireH_{wi}", headPivot,
                new Vector3(0, 0.2f - wi * 0.3f, 0), new Vector3(0.75f, 0.01f, 0.75f),
                cyan * 0.4f, true);
        }

        // Scan panel (MoodMorph UI representation)
        var panel = CreateCube("ScanPanel", root, new Vector3(0, 1.25f, 0.85f),
            new Vector3(1.5f, 0.8f, 0.04f), new Color(0.05f, 0.1f, 0.2f), false);
        panel.AddComponent<BoxCollider>();
        AddEmission(panel.GetComponent<Renderer>(), new Color(0f, 0.1f, 0.3f));

        // Panel label
        var panelLabel = CreateTextMesh("PanelLabel", panel.transform,
            new Vector3(0, 0.15f, -0.5f), 7, cyan, "MoodMorph Plugin\n[Click to Scan]");
        panelLabel.anchor = TextAnchor.MiddleCenter;

        // Panel lines (UI aesthetic)
        for (int pl = 0; pl < 4; pl++)
        {
            var pline = CreateCube($"PanelLine_{pl}", panel.transform,
                new Vector3(0, 0.1f - pl * 0.08f, -0.5f), new Vector3(0.8f, 0.005f, 0.01f),
                cyan * 0.35f, false);
        }

        // AU label objects (floating around head)
        var auLabels = new GameObject[3];
        Vector3[] auPositions = {
            new Vector3(-1.2f, 3.4f, 0), new Vector3(1.2f, 3.0f, 0.2f), new Vector3(0, 2.0f, 1f)
        };
        for (int i = 0; i < 3; i++)
        {
            var auGO  = new GameObject($"AU_Label_{i}");
            auGO.transform.SetParent(root);
            auGO.transform.localPosition = auPositions[i];
            var auBg = CreateCube($"AUBg_{i}", auGO.transform, Vector3.zero,
                new Vector3(1.4f, 0.4f, 0.02f), new Color(0f, 0.1f, 0.2f, 0.9f), false);
            auBg.AddComponent<BoxCollider>();
            AddEmission(auBg.GetComponent<Renderer>(), new Color(0f, 0.08f, 0.2f));
            var tm = CreateTextMesh($"AUText_{i}", auGO.transform, new Vector3(0, 0, -0.2f),
                6, cyan, "");
            tm.anchor = TextAnchor.MiddleCenter;
            auLabels[i] = auGO;
        }

        // Scan line
        var scanLine = CreateCube("ScanLine", headPivot, new Vector3(0, 0, 0),
            new Vector3(1.2f, 0.025f, 1.2f), new Color(0f, 1f, 1f, 0.8f), true);
        scanLine.SetActive(false);

        // Wire component
        fcp.holoHead       = headPivot;
        fcp.scanPanel      = panel;
        fcp.auLabelObjects = auLabels;
        fcp.scanLineObject = scanLine;
        fcp.headRenderer   = skull.GetComponent<Renderer>();

        CreateExhibitLabel(root, "Face Calibration Pod", "Blend-Shape → AU Mapping — Figure 2", cyan);
        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  3. EMOTION FIDELITY DISPLAY
    // ─────────────────────────────────────────────────────────────────────────
    Transform BuildEmotionFidelityDisplay(Vector3 origin)
    {
        var root = CreateRoot("EmotionFidelityDisplay_Exhibit", origin);
        var efd  = root.gameObject.AddComponent<EmotionFidelityDisplay>();

        Color neon    = new Color(0f, 1f, 0.53f);   // #00FF88
        Color dark    = new Color(0.04f, 0.09f, 0.16f);

        // Monitor stand
        var stand = CreateCylinder("Stand", root, new Vector3(0, 0.9f, 0),
            new Vector3(0.12f, 1.8f, 0.12f), new Color(0.3f, 0.3f, 0.35f), false);
        var standBase = CreateCylinder("StandBase", root, new Vector3(0, 0.05f, 0),
            new Vector3(0.8f, 0.1f, 0.8f), new Color(0.25f, 0.25f, 0.3f), false);

        // Monitor housing
        var monitor = CreateCube("Monitor", root, new Vector3(0, 2.8f, 0),
            new Vector3(5.5f, 3.8f, 0.18f), new Color(0.18f, 0.2f, 0.25f), false);

        // Screen surface
        var screen = CreateCube("Screen", monitor.transform, new Vector3(0, 0, -0.6f),
            new Vector3(0.9f, 0.9f, 0.04f), dark, false);
        AddEmission(screen.GetComponent<Renderer>(), dark * 2f);

        // Chart area container (for bar alignment)
        var chartArea = new GameObject("ChartArea").transform;
        chartArea.SetParent(monitor.transform);
        chartArea.localPosition = new Vector3(-0.3f, -0.05f, -0.6f);
        chartArea.localScale    = Vector3.one;

        // 7 emotion bars
        string[] emotions = { "Anger","Happiness","Surprise","Sadness","Disgust","Fear","Contempt" };
        float[]  scores   = { 3.87f, 3.41f, 3.34f, 2.89f, 2.58f, 2.52f, 2.29f };

        var barObjects  = new GameObject[7];
        var barLabels   = new TextMesh[7];

        float maxBarW   = 1.3f;
        float maxScore  = 5f;
        float rowHeight = 0.11f;
        float startY    = 0.28f;

        for (int i = 0; i < 7; i++)
        {
            float bw = maxBarW * scores[i] / maxScore;
            float y  = startY - i * rowHeight;

            var bar = CreateCube($"Bar_{emotions[i]}", chartArea,
                new Vector3(-maxBarW / 2f + bw / 2f, y, 0.01f),
                new Vector3(bw, rowHeight * 0.7f, 0.02f),
                neon, true);
            bar.AddComponent<BoxCollider>();
            barObjects[i] = bar;

            // Emotion label to the left
            var lbl = CreateTextMesh($"BarLabel_{i}", chartArea,
                new Vector3(-maxBarW / 2f - 0.35f, y, 0.01f),
                5, neon, emotions[i]);
            lbl.anchor = TextAnchor.MiddleRight;
            barLabels[i] = lbl;

            // Score value at end of bar
            var val = CreateTextMesh($"BarVal_{i}", chartArea,
                new Vector3(-maxBarW / 2f + bw + 0.07f, y, 0.01f),
                5, neon, scores[i].ToString("0.00"));
            val.anchor = TextAnchor.MiddleLeft;
        }

        // Mean line
        float meanX = -maxBarW / 2f + maxBarW * (2.99f / maxScore);
        var meanLine = CreateCube("MeanLine", chartArea,
            new Vector3(meanX, 0f, 0.015f),
            new Vector3(0.008f, 0.85f, 0.01f),
            Color.white, true);

        var meanLbl = CreateTextMesh("MeanLabel", chartArea,
            new Vector3(meanX + 0.07f, 0.35f, 0.01f), 5, Color.white, "μ=2.99");
        meanLbl.anchor = TextAnchor.MiddleLeft;

        // Title
        var titleLabel = CreateTextMesh("Title", monitor.transform,
            new Vector3(0, 0.42f, -1f), 7, neon, "FIDELITY ASSESSMENT\nLikert-5 Scale");
        titleLabel.anchor = TextAnchor.UpperCenter;

        // Axis labels
        var xLabel = CreateTextMesh("XAxisLabel", chartArea,
            new Vector3(0, -0.42f, 0.01f), 5, neon * 0.7f, "Fidelity Score (Likert-5)");
        xLabel.anchor = TextAnchor.MiddleCenter;

        // Tooltip
        var tooltip = CreateTextMesh("TooltipLabel", monitor.transform,
            new Vector3(0.55f, 0f, -1f), 6, Color.white, "");
        tooltip.anchor = TextAnchor.MiddleLeft;
        tooltip.gameObject.SetActive(false);

        // Wire
        efd.barObjects    = barObjects;
        efd.barLabels     = barLabels;
        efd.tooltipLabel  = tooltip;
        efd.meanLineObject= meanLine;
        efd.titleLabel    = titleLabel;
        efd.maxBarWidth   = maxBarW;
        efd.maxScore      = maxScore;

        CreateExhibitLabel(root, "Emotion Fidelity Display", "Fidelity Results — Likert-5 Scale",neon);
        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  4. FACS CODEX
    // ─────────────────────────────────────────────────────────────────────────
    Transform BuildFACSCodex(Vector3 origin)
    {
        var root = CreateRoot("FACSCodex_Exhibit", origin);
        var fc   = root.gameObject.AddComponent<FACSCodex>();

        Color cyan = new Color(0f, 0.83f, 1f);

        // Base pedestal
        var pedestal = CreateCylinder("Pedestal", root, new Vector3(0, 0.6f, 0),
            new Vector3(1.5f, 1.2f, 1.5f), cyan * 0.2f, false);

        // Bust pivot
        var bustPivot = new GameObject("BustPivot").transform;
        bustPivot.SetParent(root);
        bustPivot.localPosition = new Vector3(0, 2.2f, 0);

        // Head approximation
        var head = CreateSphere("Head", bustPivot, new Vector3(0, 0.15f, 0),
            new Vector3(0.75f, 0.9f, 0.75f), new Color(0f, 0.7f, 0.9f, 0.5f), true);
        var jaw  = CreateSphere("Jaw", bustPivot, new Vector3(0, -0.35f, 0.05f),
            new Vector3(0.5f, 0.32f, 0.5f), new Color(0f, 0.55f, 0.75f, 0.45f), true);
        var neck = CreateCylinder("Neck", bustPivot, new Vector3(0, -0.65f, 0),
            new Vector3(0.22f, 0.35f, 0.22f), cyan * 0.35f, false);
        var shoulderL = CreateSphere("ShoulderL", bustPivot, new Vector3(-0.5f, -0.9f, 0),
            new Vector3(0.5f, 0.3f, 0.4f), new Color(0f, 0.4f, 0.6f, 0.4f), false);
        var shoulderR = CreateSphere("ShoulderR", bustPivot, new Vector3(0.5f, -0.9f, 0),
            new Vector3(0.5f, 0.3f, 0.4f), new Color(0f, 0.4f, 0.6f, 0.4f), false);

        // AU muscle highlight zones (thin discs on head surface)
        string[] auZoneNames = {
            "BrowLowerer","CheekRaiser","UpperLidRaiser","LipCornerPuller",
            "JawDrop","LipCornerDepressor","UpperLipRaiser","NoseWrinkler","Dimpler"
        };
        Vector3[] auPositions = {
            new Vector3(0, 0.35f, 0.55f),    // brow
            new Vector3(0.25f, 0.05f, 0.6f),  // cheek
            new Vector3(0.15f, 0.3f, 0.6f),   // upper lid
            new Vector3(0.18f,-0.15f, 0.6f),  // lip corner
            new Vector3(0, -0.38f, 0.5f),      // jaw drop
            new Vector3(0.15f,-0.2f, 0.58f),  // lip corner dep
            new Vector3(0, -0.1f, 0.65f),      // upper lip
            new Vector3(0, 0.0f, 0.65f),       // nose
            new Vector3(0.2f,-0.15f, 0.62f)   // dimpler
        };

        var auHighlights = new GameObject[auZoneNames.Length];
        for (int i = 0; i < auZoneNames.Length; i++)
        {
            var auDisc = CreateSphere($"AU_{auZoneNames[i]}", bustPivot,
                auPositions[i], new Vector3(0.12f, 0.08f, 0.12f),
                cyan, true);
            auDisc.SetActive(false);
            auHighlights[i] = auDisc;
        }

        // 7 emotion chips (hex approximated as flattened cylinders)
        string[] emotions = { "Anger","Happiness","Surprise","Sadness","Disgust","Fear","Contempt" };
        var chips = new GameObject[7];
        for (int i = 0; i < 7; i++)
        {
            var chip = new GameObject($"Chip_{emotions[i]}");
            chip.transform.SetParent(root);
            chip.transform.localPosition = Vector3.zero;

            var chipBody = CreateCylinder("Body", chip.transform, Vector3.zero,
                new Vector3(0.35f, 0.06f, 0.35f), cyan * 0.5f, true);
            chipBody.AddComponent<CapsuleCollider>();
            var tm = CreateTextMesh("Label", chip.transform, new Vector3(0, 0.07f, 0),
                6, cyan, emotions[i]);
            tm.anchor = TextAnchor.MiddleCenter;
            chips[i] = chip;
        }

        // Info label
        var infoLabel = CreateTextMesh("InfoLabel", root,
            new Vector3(0, 4.5f, 0), 8, Color.white, "");
        infoLabel.anchor = TextAnchor.UpperCenter;
        infoLabel.gameObject.SetActive(false);

        // Title
        var titleLabel = CreateTextMesh("TitleLabel", root,
            new Vector3(0, 5.2f, 0), 9, cyan,
            "Ekman's FACS\nShared vocabulary between OpenFace and LLM");
        titleLabel.anchor = TextAnchor.UpperCenter;

        // Wire
        fc.bust         = bustPivot;
        fc.emotionChips = chips;
        fc.auHighlights = auHighlights;
        fc.infoLabel    = infoLabel;
        fc.titleLabel   = titleLabel;

        CreateExhibitLabel(root, "FACS Codex", "Action Units & Emotion Mapping", cyan);
        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  5. LIMITATION SHARD
    // ─────────────────────────────────────────────────────────────────────────
    Transform BuildLimitationShard(Vector3 origin)
    {
        var root = CreateRoot("LimitationShard_Exhibit", origin);
        var ls   = root.gameObject.AddComponent<LimitationShard>();

        Color crystalCol = new Color(0.29f, 0.33f, 0.41f);
        Color crackCol   = new Color(1f, 0.27f, 0.27f);

        // Crystal pivot (levitating)
        var crystalPivot = new GameObject("CrystalPivot").transform;
        crystalPivot.SetParent(root);
        crystalPivot.localPosition = new Vector3(0, 2.5f, 0);

        // Main crystal body (4 facets using rotated cubes)
        var crystalBody = CreateCube("CrystalBody", crystalPivot, Vector3.zero,
            new Vector3(0.8f, 1.4f, 0.8f), crystalCol, false);
        crystalBody.transform.localRotation = Quaternion.Euler(0, 45f, 15f);
        var crystalBodyR = crystalBody.GetComponent<Renderer>();
        crystalBodyR.material.color = crystalCol;
        // semi-transparent
        SetTransparent(crystalBodyR.material, new Color(crystalCol.r, crystalCol.g, crystalCol.b, 0.7f));

        var shard1 = CreateCube("Shard1", crystalPivot, new Vector3(0.25f, 0.3f, 0.1f),
            new Vector3(0.35f, 0.9f, 0.35f), crystalCol, false);
        shard1.transform.localRotation = Quaternion.Euler(20f, 30f, -10f);
        SetTransparent(shard1.GetComponent<Renderer>().material,
            new Color(crystalCol.r, crystalCol.g, crystalCol.b, 0.55f));

        var shard2 = CreateCube("Shard2", crystalPivot, new Vector3(-0.2f, -0.2f, 0.15f),
            new Vector3(0.3f, 0.7f, 0.3f), crystalCol, false);
        shard2.transform.localRotation = Quaternion.Euler(-15f, -20f, 5f);
        SetTransparent(shard2.GetComponent<Renderer>().material,
            new Color(crystalCol.r, crystalCol.g, crystalCol.b, 0.5f));

        var shard3 = CreateCube("Shard3", crystalPivot, new Vector3(0.1f, -0.5f, -0.2f),
            new Vector3(0.25f, 0.55f, 0.25f), crystalCol, false);
        shard3.transform.localRotation = Quaternion.Euler(10f, 60f, 20f);
        SetTransparent(shard3.GetComponent<Renderer>().material,
            new Color(crystalCol.r, crystalCol.g, crystalCol.b, 0.45f));

        // Crack lines (thin emissive quads cutting through)
        var crack1 = CreateCube("Crack1", crystalPivot, new Vector3(0.1f, 0, 0.05f),
            new Vector3(0.6f, 0.01f, 0.6f), crackCol, true);
        crack1.transform.localRotation = Quaternion.Euler(30f, 10f, 0);
        var crack2 = CreateCube("Crack2", crystalPivot, new Vector3(-0.05f, 0.2f, 0),
            new Vector3(0.5f, 0.01f, 0.5f), crackCol, true);
        crack2.transform.localRotation = Quaternion.Euler(-20f, 50f, 5f);

        // Floating base platform
        var platform = CreateCylinder("Platform", root, new Vector3(0, 0.08f, 0),
            new Vector3(1.2f, 0.08f, 1.2f), new Color(0.2f, 0.22f, 0.28f), false);

        // 3 limitation bubbles
        string[] titles = { "⏱  Latency", "?  Confidence", "⚠  LLM Drift" };
        string[] shorts = {
            "RAG not suitable for\nreal-time animation\nAvg 8.2s / Max 25.3s",
            "Confidence threshold ≠\nfidelity correlation",
            "LLM selects blend shapes\nby name, not AU mapping"
        };
        string[] details = {
            "Average: 8.2s | Max: 25.3s\nImpractical for live gameplay\nrequiring frame-rate animation.",
            "No statistically significant\ncorrelation found between\nAU confidence and quality.",
            "LLM sometimes bypasses\nAU data and picks blend\nshapes by suggestive names."
        };

        var bubbles     = new GameObject[3];
        var bubbleTexts = new TextMesh[3];
        var detailTexts = new TextMesh[3];

        for (int i = 0; i < 3; i++)
        {
            var bubble = new GameObject($"Bubble_{i}");
            bubble.transform.SetParent(root);
            bubble.transform.localPosition = Vector3.zero;

            var bg = CreateCube("BubbleBg", bubble.transform, Vector3.zero,
                new Vector3(1.6f, 0.8f, 0.04f), new Color(0.1f, 0.12f, 0.18f, 0.92f), false);
            bg.AddComponent<BoxCollider>();
            AddEmission(bg.GetComponent<Renderer>(), new Color(0.05f, 0.07f, 0.15f));

            var btext = CreateTextMesh("BubbleText", bubble.transform,
                new Vector3(0, 0, -0.3f), 6, new Color(0.7f, 0.8f, 1f), titles[i]+"\n"+shorts[i]);
            btext.anchor = TextAnchor.MiddleCenter;

            var detailBg = CreateCube("DetailBg", bubble.transform, new Vector3(0, -0.95f, 0),
                new Vector3(1.8f, 0.75f, 0.04f), new Color(0.05f, 0.05f, 0.1f, 0.95f), false);
            AddEmission(detailBg.GetComponent<Renderer>(), new Color(0.1f, 0.03f, 0.03f));

            var dtext = CreateTextMesh("DetailText", bubble.transform,
                new Vector3(0, -0.95f, -0.3f), 6, new Color(1f, 0.7f, 0.7f), details[i]);
            dtext.anchor = TextAnchor.MiddleCenter;
            dtext.gameObject.SetActive(false);

            bubbles[i]     = bubble;
            bubbleTexts[i] = btext;
            detailTexts[i] = dtext;
        }

        // Future work label below crystal
        var futureLabel = CreateTextMesh("FutureWork", root,
            new Vector3(0, -0.2f, 0), 6, new Color(0.5f, 0.6f, 0.8f),
            "Future work: micro-expressions · hybrid procedural+AI · ARKit integration");
        futureLabel.anchor = TextAnchor.UpperCenter;

        // Wire
        ls.crystal        = crystalPivot;
        ls.bubbleObjects  = bubbles;
        ls.bubbleTexts    = bubbleTexts;
        ls.detailTexts    = detailTexts;
        ls.crystalRenderer= crystalBody.GetComponent<Renderer>();
        ls.crackRenderers = new Renderer[] { crack1.GetComponent<Renderer>(), crack2.GetComponent<Renderer>() };

        CreateExhibitLabel(root, "Limitation Shard", "Threats to Validity — Section VII",
            new Color(0.6f, 0.7f, 0.9f));
        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CAMERA SETUP
    // ─────────────────────────────────────────────────────────────────────────
    void SetupCamera(List<Transform> exhibits)
    {
        var camGO = Camera.main ? Camera.main.gameObject : new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        if (!camGO.GetComponent<Camera>()) camGO.AddComponent<Camera>();

        var nav = camGO.GetComponent<SceneNavigator>() ?? camGO.AddComponent<SceneNavigator>();

        nav.exhibitTargets = new Transform[exhibits.Count];
        nav.exhibitNames   = new string[exhibits.Count];
        string[] names = {
            "Pipeline Terminal", "Face Calibration Pod",
            "Emotion Fidelity Display", "FACS Codex", "Limitation Shard"
        };
        for (int i = 0; i < exhibits.Count; i++)
        {
            nav.exhibitTargets[i] = exhibits[i];
            nav.exhibitNames[i]   = i < names.Length ? names[i] : exhibits[i].name;
        }

        nav.focusDistance  = 7f;
        nav.transitionTime = 0.8f;

        // Position camera at first exhibit
        camGO.transform.position = new Vector3(0, 3f, -9f);
        camGO.transform.LookAt(exhibits[0].position + Vector3.up);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LIGHTING
    // ─────────────────────────────────────────────────────────────────────────
    void SetupLighting()
    {
        // Ambient
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.12f);
        RenderSettings.fogColor     = new Color(0.02f, 0.03f, 0.06f);
        RenderSettings.fog          = true;
        RenderSettings.fogMode      = FogMode.Linear;
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance   = 70f;

        // Main directional
        var dirLight = FindOrCreate<Light>("MainLight");
        dirLight.type      = LightType.Directional;
        dirLight.color     = new Color(0.9f, 0.85f, 0.7f);
        dirLight.intensity = 0.6f;
        dirLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // Fill point light per exhibit
        for (int i = 0; i < 5; i++)
        {
            var pt = new GameObject($"PointLight_{i}");
            pt.transform.position = new Vector3(i * Spacing, 4f, 0);
            var l = pt.AddComponent<Light>();
            l.type  = LightType.Point;
            l.range = 10f;
            l.intensity = 1.5f;
            l.color = i == 0 ? new Color(1f, 0.72f, 0f) :
                      i == 1 ? new Color(0f, 0.83f, 1f) :
                      i == 2 ? new Color(0f, 1f, 0.53f) :
                      i == 3 ? new Color(0f, 0.83f, 1f) :
                                new Color(0.6f, 0.65f, 0.9f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FLOOR
    // ─────────────────────────────────────────────────────────────────────────
    void SetupFloor(int count)
    {
        float totalWidth = (count - 1) * Spacing + 10f;
        var floor = CreateCube("ExhibitionFloor", transform,
            new Vector3((count - 1) * Spacing / 2f, -0.05f, 0),
            new Vector3(totalWidth, 0.1f, 8f),
            new Color(0.07f, 0.08f, 0.12f), false);
        // Grid lines
        for (int i = 0; i < count; i++)
        {
            var mark = CreateCube($"FloorMark_{i}", floor.transform,
                new Vector3((-totalWidth / 2f + i * Spacing / totalWidth * totalWidth + Spacing * 0.5f) / totalWidth,
                    0.51f, 0),
                new Vector3(0.003f, 1f, 1f),
                new Color(0.2f, 0.25f, 0.4f, 0.3f), false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────
    Transform CreateRoot(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.localPosition = pos;
        return go.transform;
    }

    GameObject CreateCube(string name, Transform parent, Vector3 localPos, Vector3 scale,
        Color color, bool emissive)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        go.transform.localRotation = Quaternion.identity;
        ApplyColor(go, color, emissive);
        return go;
    }

    GameObject CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 scale,
        Color color, bool emissive)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        go.transform.localRotation = Quaternion.identity;
        ApplyColor(go, color, emissive);
        return go;
    }

    GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 scale,
        Color color, bool emissive)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        go.transform.localRotation = Quaternion.identity;
        ApplyColor(go, color, emissive);
        return go;
    }

    GameObject CreateTorus(string name, Transform parent, Vector3 localPos, Color color)
    {
        // Approximate torus with cylinder ring
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        int segments = 12;
        float radius = 0.82f;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * 360f / segments;
            float rad   = angle * Mathf.Deg2Rad;
            var seg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            seg.transform.SetParent(go.transform);
            seg.transform.localPosition =
                new Vector3(Mathf.Cos(rad) * radius, 0, Mathf.Sin(rad) * radius);
            seg.transform.localScale = new Vector3(0.12f, 0.06f, 0.12f);
            ApplyColor(seg, color, true);
        }
        return go;
    }

    TextMesh CreateTextMesh(string name, Transform parent, Vector3 localPos,
        int fontSize, Color color, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one * 0.1f;
        var tm = go.AddComponent<TextMesh>();
        tm.text      = text;
        tm.fontSize  = fontSize * 10;
        tm.color     = color;
        tm.anchor    = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        return tm;
    }

    void CreateExhibitLabel(Transform root, string title, string subtitle, Color color)
    {
        var t = CreateTextMesh("ExhibitTitle", root, new Vector3(0, -0.5f, 1.5f),
            10, color, title);
        t.anchor = TextAnchor.UpperCenter;
        var s = CreateTextMesh("ExhibitSubtitle", root, new Vector3(0, -0.9f, 1.5f),
            7, color * 0.75f, subtitle);
        s.anchor = TextAnchor.UpperCenter;
    }

    void ApplyColor(GameObject go, Color color, bool emissive)
    {
        var r = go.GetComponent<Renderer>();
        if (!r) return;
        var mat = r.material;
        mat.color = color;
        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.6f);
        }
    }

    void AddEmission(Renderer r, Color emColor)
    {
        if (!r) return;
        r.material.EnableKeyword("_EMISSION");
        r.material.SetColor("_EmissionColor", emColor);
    }

    void SetTransparent(Material mat, Color color)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = color;
    }

    T FindOrCreate<T>(string name) where T : Component
    {
        var found = FindFirstObjectByType<T>();
        if (found) return found;
        var go = new GameObject(name);
        return go.AddComponent<T>();
    }
}
