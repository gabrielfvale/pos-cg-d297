using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using EditorSceneManagement = UnityEditor.SceneManagement;

[ExecuteInEditMode]
public class ClaudeTestSceneBuilder : MonoBehaviour
{
    const float RADIUS = 8f;
    const float PANEL_W = 700f;
    const float PANEL_H = 480f;
    const float PANEL_SCALE = 0.012f;

    static readonly Color[] BorderColors = {
        new Color(1f, 0.72f, 0f),
        new Color(0f, 0.83f, 1f),
        new Color(0f, 1f, 0.53f),
        new Color(0.9f, 0.9f, 1f),
        new Color(0.45f, 0.52f, 0.65f)
    };
    static readonly Color[] BgColors = {
        new Color(0.06f, 0.09f, 0.16f, 0.97f),
        new Color(0.03f, 0.09f, 0.18f, 0.97f),
        new Color(0.03f, 0.07f, 0.12f, 0.97f),
        new Color(0.04f, 0.05f, 0.14f, 0.97f),
        new Color(0.10f, 0.13f, 0.20f, 0.88f)
    };
    static readonly string[] TypeLabels = {
        "01 — CONTRIBUIÇÃO CENTRAL",
        "02 — MÉTODO",
        "03 — MÉTRICAS",
        "04 — RESULTADO",
        "05 — LIMITAÇÃO"
    };

    [ContextMenu("Build ClaudeTest Scene")]
    public void BuildScene()
    {
        SetupEnvironment();
        BuildPanel_RAGPipeline(0);
        BuildPanel_BlendshapeFlow(1);
        BuildPanel_FidelityChart(2);
        BuildPanel_CharacterGallery(3);
        BuildPanel_Limitation(4);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("[ClaudeTest] Scene built — 5 panels in circle.");
    }

    void SetupEnvironment()
    {
        // Clear old panels
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects())
            if (r.name.StartsWith("Panel_") || r.name == "EventSystem")
                DestroyImmediate(r);

        // EventSystem — prefer new Input System module
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        var inputSysType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSysType != null)
            esGO.AddComponent(inputSysType);
        else
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Camera at origin, add orbit controller
        var cam = Camera.main;
        if (cam == null)
        {
            var cGO = new GameObject("Main Camera");
            cGO.tag = "MainCamera";
            cam = cGO.AddComponent<Camera>();
        }
        cam.transform.position = Vector3.zero;
        cam.transform.rotation = Quaternion.identity;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
        cam.backgroundColor = new Color(0.04f, 0.09f, 0.15f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        var oldOrbit = cam.GetComponent<CameraOrbitController>();
        if (oldOrbit != null) DestroyImmediate(oldOrbit);
        cam.gameObject.AddComponent<CameraOrbitController>();

        // Light
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light dir = null;
        foreach (var l in lights) if (l.type == LightType.Directional) { dir = l; break; }
        if (dir == null) dir = new GameObject("Directional Light").AddComponent<Light>();
        dir.type = LightType.Directional;
        dir.color = new Color(0.6f, 0.65f, 0.9f);
        dir.intensity = 0.5f;
        dir.transform.rotation = Quaternion.Euler(45, -30, 0);

        RenderSettings.skybox = null;
        RenderSettings.ambientLight = new Color(0.1f, 0.12f, 0.22f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    void BuildPanel_RAGPipeline(int index)
    {
        var panel = SpawnPanel("Panel_RAGPipeline", index);
        var rag = panel.content.AddComponent<RAGPipelinePanel>();
        rag.fig1Sprite = FigureLoader.Load("FIG1");
    }

    void BuildPanel_BlendshapeFlow(int index)
    {
        var panel = SpawnPanel("Panel_BlendshapeFlow", index);
        panel.content.AddComponent<BlendshapeFlowPanel>();
    }

    void BuildPanel_FidelityChart(int index)
    {
        var panel = SpawnPanel("Panel_FidelityChart", index);
        panel.content.AddComponent<FidelityChartPanel>();
    }

    void BuildPanel_CharacterGallery(int index)
    {
        var panel = SpawnPanel("Panel_CharacterGallery", index);
        var gallery = panel.content.AddComponent<CharacterGalleryPanel>();
        gallery.fig3Sprite = FigureLoader.Load("FIG3");
    }

    void BuildPanel_Limitation(int index)
    {
        var panel = SpawnPanel("Panel_Limitation", index);
        panel.content.AddComponent<LimitationPanel>();
    }

    (GameObject root, GameObject content) SpawnPanel(string panelName, int index)
    {
        float angle = index * (360f / 5);
        float rad = angle * Mathf.Deg2Rad;
        var pos = new Vector3(Mathf.Sin(rad) * RADIUS, 0f, Mathf.Cos(rad) * RADIUS);
        var lookRot = Quaternion.LookRotation(-pos.normalized, Vector3.up);

        var go = new GameObject(panelName);
        go.transform.position = pos;
        go.transform.rotation = lookRot;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        go.AddComponent<GraphicRaycaster>();

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(PANEL_W, PANEL_H);
        // Negative X scale compensates for the X-axis flip that LookRotation(-pos) introduces,
        // so UI text and layout read correctly when viewed from the center (camera at origin).
        rt.localScale = new Vector3(-PANEL_SCALE, PANEL_SCALE, PANEL_SCALE);

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bgGO.AddComponent<Image>().color = BgColors[index];
        var outline = bgGO.AddComponent<Outline>();
        outline.effectColor = BorderColors[index];
        outline.effectDistance = new Vector2(3, 3);
        outline.useGraphicAlpha = false;

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(go.transform, false);
        var cRT = contentGO.AddComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        cRT.offsetMin = cRT.offsetMax = Vector2.zero;

        // Type label above
        var lbl = new GameObject("TypeLabel");
        lbl.transform.SetParent(go.transform, false);
        var lblRT = lbl.AddComponent<RectTransform>();
        lblRT.anchorMin = new Vector2(0, 1); lblRT.anchorMax = new Vector2(1, 1);
        lblRT.pivot = new Vector2(0.5f, 0f);
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = new Vector2(0, 26);
        var lblTxt = lbl.AddComponent<Text>();
        lblTxt.text = TypeLabels[index];
        lblTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lblTxt.fontSize = 12;
        lblTxt.color = new Color(BorderColors[index].r, BorderColors[index].g, BorderColors[index].b, 0.7f);
        lblTxt.alignment = TextAnchor.MiddleCenter;
        lblTxt.fontStyle = FontStyle.Bold;

        return (go, contentGO);
    }
}
