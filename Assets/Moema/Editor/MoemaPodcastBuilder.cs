using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-shot builder for the "Moema podcast" scene.
///
/// It (1) configures the FBX importers (generic rig + looping idle clip),
/// (2) creates a looping Animator controller and a lit material that adapts to
/// the active render pipeline, (3) builds a self-contained scene where the Moema
/// rig plays the idle animation framed close on its face, with a visual-novel
/// dialogue box and the podcast audio, (4) registers the scene in Build Settings,
/// and (5) writes a preview PNG to the project root for verification.
///
/// Run it from the menu (Tools ▸ Moema ▸ Build Podcast Scene) or in batch mode:
///   Unity -batchmode -quit -projectPath . -executeMethod MoemaPodcastBuilder.Build
/// </summary>
public static class MoemaPodcastBuilder
{
    private const string RigPath = "Assets/Moema/Models/Moema_compressed.fbx";
    private const string IdlePath = "Assets/Moema/Models/Moema_compressed_idle.fbx";
    private const string AudioPath = "Assets/Moema/Audio/MoemaMoemoPodcast.mp3";
    private const string ControllerPath = "Assets/Moema/Animation/MoemaIdle.controller";
    private const string MaterialPath = "Assets/Moema/Materials/Moema.mat";
    private const string ScenePath = "Assets/Scenes/MoemaPodcast.unity";

    // Side of the head the camera sits on. Verified from the preview renders:
    // Moema's face is on the +Z side, opposite the toe-forward direction, so -1.
    private const float FaceSign = -1f;

    private static readonly Color BackgroundColor = new Color(0.078f, 0.090f, 0.137f, 1f);
    private static readonly Color SkinColor = new Color(0.86f, 0.78f, 0.70f, 1f);

    [MenuItem("Tools/Moema/Build Podcast Scene")]
    public static void Build()
    {
        try
        {
            Debug.Log("[MoemaBuilder] === Build started ===");

            ConfigureImporters();
            AnimationClip idleClip = LoadIdleClip();
            AnimatorController controller = CreateController(idleClip);
            Material bodyMaterial = CreateBodyMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureEnvironment();
            Camera cam = CreateCamera();
            CreateLights();

            GameObject character = InstantiateCharacter(controller, bodyMaterial, idleClip);
            Framing framing = ComputeFraming(character);
            ApplyFraming(cam, framing, FaceSign);

            BuildDialogueUI(out CanvasGroup panelGroup, out TMP_Text nameText,
                            out TMP_Text bodyTMP, out Image nameBg);
            AudioSource audio = CreateAudio();
            WireController(audio, panelGroup, nameText, bodyTMP, nameBg);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[MoemaBuilder] scene saved={saved} path={ScenePath}");

            RegisterSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Render the chosen framing plus both facing candidates so the front
            // can be confirmed visually (the model is untextured, so a single
            // angle is ambiguous). The saved scene keeps the FaceSign camera.
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ApplyFraming(cam, framing, 1f);
            TryCapturePreview(cam, Path.Combine(root, "moema_preview_front.png"));
            ApplyFraming(cam, framing, -1f);
            TryCapturePreview(cam, Path.Combine(root, "moema_preview_back.png"));
            ApplyFraming(cam, framing, FaceSign);
            TryCapturePreview(cam, Path.Combine(root, "moema_preview.png"));

            // Full-composition preview (character + dialogue box) for verification.
            Canvas dlgCanvas = panelGroup != null ? panelGroup.GetComponentInParent<Canvas>() : null;
            if (dlgCanvas != null)
                TryCaptureComposition(cam, dlgCanvas, panelGroup, bodyTMP, nameText,
                                      Path.Combine(root, "moema_composition.png"));

            Debug.Log("[MoemaBuilder] === MOEMA_BUILD_OK ===");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[MoemaBuilder] BUILD FAILED: " + e + "\n" + e.StackTrace);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    // ----------------------------------------------------------- importers ---

    private static void ConfigureImporters()
    {
        var rig = (ModelImporter)AssetImporter.GetAtPath(RigPath);
        if (rig == null) throw new FileNotFoundException("Rig FBX not found at " + RigPath);
        rig.animationType = ModelImporterAnimationType.Generic;
        rig.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        rig.importAnimation = false;
        rig.materialImportMode = ModelImporterMaterialImportMode.None;
        rig.SaveAndReimport();

        var idle = (ModelImporter)AssetImporter.GetAtPath(IdlePath);
        if (idle == null) throw new FileNotFoundException("Idle FBX not found at " + IdlePath);
        idle.animationType = ModelImporterAnimationType.Generic;
        idle.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        idle.importAnimation = true;
        idle.materialImportMode = ModelImporterMaterialImportMode.None;

        ModelImporterClipAnimation[] clips = idle.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = true;
                clips[i].loopPose = true;
            }
            idle.clipAnimations = clips;
        }
        idle.SaveAndReimport();
        Debug.Log("[MoemaBuilder] importers configured (generic rig + looping idle).");
    }

    private static AnimationClip LoadIdleClip()
    {
        AnimationClip clip = null;
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(IdlePath))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__"))
            {
                clip = c;
                break;
            }
        }
        if (clip == null)
            throw new System.Exception("No AnimationClip found inside " + IdlePath);
        Debug.Log($"[MoemaBuilder] idle clip = '{clip.name}' length={clip.length:0.00}s");
        return clip;
    }

    private static AnimatorController CreateController(AnimationClip idleClip)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, idleClip);

        // Make sure the default state loops.
        AnimatorState defaultState = controller.layers[0].stateMachine.defaultState;
        if (defaultState != null)
        {
            defaultState.motion = idleClip;
            defaultState.name = "Idle";
        }
        EditorUtility.SetDirty(controller);
        Debug.Log("[MoemaBuilder] animator controller created at " + ControllerPath);
        return controller;
    }

    // ------------------------------------------------------------ material ---

    private static Material CreateBodyMaterial()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));

        bool srpActive = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = srpActive ? Shader.Find("Universal Render Pipeline/Lit") : null;
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader) { name = "Moema" };
        // Set whichever colour property the chosen shader exposes.
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", SkinColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", SkinColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

        AssetDatabase.CreateAsset(mat, MaterialPath);
        Debug.Log($"[MoemaBuilder] material created with shader '{shader.name}' (SRP active={srpActive}).");
        return mat;
    }

    // --------------------------------------------------------- environment ---

    private static void ConfigureEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.42f, 1f);
        RenderSettings.fog = false;
    }

    private static Camera CreateCamera()
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.01f;
        camGO.AddComponent<AudioListener>();
        return cam;
    }

    private static void CreateLights()
    {
        var keyGO = new GameObject("Key Light");
        var key = keyGO.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.15f;
        key.color = new Color(1f, 0.97f, 0.92f);
        key.shadows = LightShadows.Soft;
        keyGO.transform.rotation = Quaternion.Euler(45f, 30f, 0f);

        var fillGO = new GameObject("Fill Light");
        var fill = fillGO.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.45f;
        fill.color = new Color(0.7f, 0.8f, 1f);
        fillGO.transform.rotation = Quaternion.Euler(20f, 210f, 0f);
    }

    // ------------------------------------------------------------ character ---

    private static GameObject InstantiateCharacter(AnimatorController controller, Material bodyMaterial,
                                                    AnimationClip idleClip)
    {
        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
        if (rigAsset == null) throw new FileNotFoundException("Could not load rig asset " + RigPath);

        var character = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset);
        character.name = "Moema";
        character.transform.position = Vector3.zero;
        character.transform.rotation = Quaternion.identity;

        var animator = character.GetComponent<Animator>();
        if (animator == null) animator = character.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        foreach (Renderer r in character.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = bodyMaterial;
            r.sharedMaterials = mats;
        }

        VerifyBinding(character, idleClip);
        return character;
    }

    /// <summary>Logs how many of the clip's curve paths resolve in the instantiated rig.</summary>
    private static void VerifyBinding(GameObject character, AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        var paths = new HashSet<string>(bindings.Select(b => b.path));
        int matched = paths.Count(p => character.transform.Find(p) != null);
        Debug.Log($"[MoemaBuilder] animation binding: {matched}/{paths.Count} clip paths resolve in the rig.");
        if (paths.Count > 0 && matched == 0)
            Debug.LogWarning("[MoemaBuilder] No clip paths resolved — animation may not play. " +
                             "Check that both FBXs share the same skeleton root.");
    }

    private struct Framing
    {
        public Vector3 aim;          // point the camera looks at
        public Vector3 forwardHoriz; // unsigned facing guess (horizontal)
        public Vector3 up;
        public float dist;
        public float fov;
    }

    /// <summary>
    /// Computes a head-and-shoulders ("VN portrait") framing from the skeleton,
    /// so it is independent of the unreliable skinned-mesh bounds.
    /// </summary>
    private static Framing ComputeFraming(GameObject character)
    {
        Transform head = FindBone(character, "head");
        Transform headEnd = FindBone(character, "head_end");
        Transform neck = FindBone(character, "neck");
        Transform body = FindBone(character, "body");
        Transform shL = FindBone(character, "shoulder_left");
        Transform shR = FindBone(character, "shoulder_right");
        Transform footL = FindBone(character, "leg_left_foot");
        Transform footLe = FindBone(character, "leg_left_foot_end");
        Transform footR = FindBone(character, "leg_right_foot");
        Transform footRe = FindBone(character, "leg_right_foot_end");

        DumpBone("head", head); DumpBone("head_end", headEnd); DumpBone("neck", neck);
        DumpBone("body", body); DumpBone("shoulder_left", shL); DumpBone("shoulder_right", shR);
        DumpBone("leg_left_foot", footL); DumpBone("leg_left_foot_end", footLe);
        Bounds b = ComputeBounds(character);
        Debug.Log($"[MoemaBuilder] renderer bounds center={b.center} size={b.size}");

        // Up axis from the spine if available.
        Vector3 up = (head != null && body != null) ? (head.position - body.position).normalized : Vector3.up;
        if (up.sqrMagnitude < 1e-6f) up = Vector3.up;

        // Facing (unsigned): toes point forward; fall back to shoulder line x spine.
        Vector3 fwd = Vector3.zero;
        int toes = 0;
        if (footL != null && footLe != null) { fwd += footLe.position - footL.position; toes++; }
        if (footR != null && footRe != null) { fwd += footRe.position - footR.position; toes++; }
        if (toes > 0) fwd = Vector3.ProjectOnPlane(fwd, up);
        if (fwd.sqrMagnitude < 1e-6f && shL != null && shR != null)
            fwd = Vector3.Cross(up, (shR.position - shL.position).normalized);
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
        fwd.Normalize();

        // Face centre and head size from the cranium bones only (the neck is very
        // long on this character, so neck-based sizing would zoom out too far).
        Vector3 crown = headEnd != null ? headEnd.position
                       : (head != null ? head.position + up : b.center);
        Vector3 headBase = head != null ? head.position : (neck != null ? neck.position : b.center);
        float headH = Vector3.Distance(crown, headBase);
        if (headH < 1e-3f) headH = Mathf.Max(0.001f, b.size.y * 0.10f);
        // The hair quiff reaches well above the crown bone, so anchor the top of
        // the frame to the mesh bounds and put the eyes a little above centre.
        Vector3 faceCenter = headBase;
        float visualTop = b.center.y + b.extents.y;      // top of the hair
        float eyesY = headBase.y - 0.15f * headH;        // eyes sit below the head bone
        const float eyesFrac = 0.55f;                    // eyes ~55% up the frame
        const float topFrac = 0.96f;                     // 4% headroom above the hair
        float viewHeight = Mathf.Max((visualTop - eyesY) / (topFrac - eyesFrac), headH * 2f);

        float fov = 30f;
        float dist = (viewHeight * 0.5f) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

        float bottomY = eyesY - eyesFrac * viewHeight;
        float aimY = bottomY + 0.5f * viewHeight;
        Vector3 aim = new Vector3(faceCenter.x, aimY, faceCenter.z);

        Debug.Log($"[MoemaBuilder] framing: headH={headH:0.000} viewH={viewHeight:0.000} " +
                  $"dist={dist:0.000} fwd={fwd} up={up} faceCenter={faceCenter} aim={aim}");

        return new Framing { aim = aim, forwardHoriz = fwd, up = up, dist = dist, fov = fov };
    }

    private static void ApplyFraming(Camera cam, Framing f, float sign)
    {
        Vector3 camPos = f.aim + f.forwardHoriz * sign * f.dist;
        cam.transform.position = camPos;
        cam.transform.rotation = Quaternion.LookRotation((f.aim - camPos).normalized, f.up);
        cam.fieldOfView = f.fov;
        cam.nearClipPlane = Mathf.Max(0.01f, f.dist * 0.02f);
        cam.farClipPlane = f.dist * 4f + 1000f;
    }

    private static void DumpBone(string label, Transform t)
    {
        if (t != null) Debug.Log($"[MoemaBuilder] bone {label} pos={t.position}");
        else Debug.Log($"[MoemaBuilder] bone {label} = MISSING");
    }

    private static Transform FindBone(GameObject root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }

    private static Bounds ComputeBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // ------------------------------------------------------------ audio ---

    private static AudioSource CreateAudio()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
        var go = new GameObject("PodcastAudio");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.playOnAwake = true;
        src.loop = false;
        src.spatialBlend = 0f;
        src.volume = 1f;
        if (clip == null)
            Debug.LogWarning("[MoemaBuilder] audio clip not found at " + AudioPath);
        return src;
    }

    // ------------------------------------------------------------ UI ---

    private static void BuildDialogueUI(out CanvasGroup panelGroup, out TMP_Text nameText,
                                        out TMP_Text bodyTMP, out Image nameBg)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        var canvasGO = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas),
                                      typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();

        Sprite roundedBox = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        // Dialogue panel anchored along the bottom of the screen.
        var panel = NewUI("DialoguePanel", canvasRect);
        panel.anchorMin = new Vector2(0.06f, 0.05f);
        panel.anchorMax = new Vector2(0.94f, 0.30f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.sprite = roundedBox;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.04f, 0.05f, 0.09f, 0.86f);
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        // Nameplate, sitting just above the panel's top-left corner.
        var nameplate = NewUI("Nameplate", panel);
        nameplate.anchorMin = new Vector2(0f, 1f);
        nameplate.anchorMax = new Vector2(0f, 1f);
        nameplate.pivot = new Vector2(0f, 0f);
        nameplate.anchoredPosition = new Vector2(40f, -6f);
        nameplate.sizeDelta = new Vector2(320f, 64f);
        nameBg = nameplate.gameObject.AddComponent<Image>();
        nameBg.sprite = roundedBox;
        nameBg.type = Image.Type.Sliced;
        nameBg.color = new Color(0.96f, 0.55f, 0.30f, 1f);

        var nameTextRT = NewUI("NameText", nameplate);
        Stretch(nameTextRT, 18f, 4f);
        nameText = nameTextRT.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) nameText.font = font;
        nameText.text = "Moema";
        nameText.fontSize = 30f;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.color = Color.white;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;

        // Caption body inside the panel.
        var bodyRT = NewUI("BodyText", panel);
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(48f, 36f);
        bodyRT.offsetMax = new Vector2(-48f, -54f);
        bodyTMP = bodyRT.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) bodyTMP.font = font;
        bodyTMP.text = "";
        bodyTMP.fontSize = 36f;
        bodyTMP.alignment = TextAlignmentOptions.TopLeft;
        bodyTMP.color = new Color(0.95f, 0.95f, 0.97f, 1f);
        bodyTMP.textWrappingMode = TextWrappingModes.Normal;

        if (font == null)
            Debug.LogWarning("[MoemaBuilder] TMP default font asset is null; text may be invisible until TMP Essentials are imported.");
    }

    private static RectTransform NewUI(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    private static void Stretch(RectTransform rt, float padX, float padY)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    private static void WireController(AudioSource audio, CanvasGroup panelGroup,
                                       TMP_Text nameText, TMP_Text bodyTMP, Image nameBg)
    {
        var go = new GameObject("DialogueController");
        var dc = go.AddComponent<MoemaDialogueController>();
        dc.audioSource = audio;
        dc.dialoguePanel = panelGroup;
        dc.nameplateText = nameText;
        dc.bodyText = bodyTMP;
        dc.nameplateBackground = nameBg;
        dc.lines = PlaceholderLines();
    }

    private static List<MoemaDialogueController.DialogueLine> PlaceholderLines()
    {
        // Placeholders spread across the ~3 min podcast. Replace with the real
        // transcript: set speaker, text and the start time (seconds) of each turn.
        return new List<MoemaDialogueController.DialogueLine>
        {
            New("Moema", "Olá! Eu sou a Moema. Bem-vindos ao nosso podcast.", 0f),
            New("Moemo", "E eu sou o Moemo. Hoje a conversa é só nossa!", 7f),
            New("Moema", "(Substitua estas falas pela transcrição real do áudio.)", 16f),
            New("Moemo", "Edite a lista \"Lines\" no componente DialogueController.", 28f),
            New("Moema", "Cada linha tem: locutor, texto e o tempo de início em segundos.", 45f),
            New("Moemo", "Assim a legenda acompanha o áudio do podcast.", 70f),
            New("Moema", "Vamos continuar a nossa conversa...", 100f),
            New("Moemo", "Até o final do episódio!", 150f),
        };
    }

    private static MoemaDialogueController.DialogueLine New(string speaker, string text, float time)
    {
        return new MoemaDialogueController.DialogueLine { speaker = speaker, text = text, time = time };
    }

    // ------------------------------------------------------- build settings ---

    private static void RegisterSceneInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[MoemaBuilder] scene registered in Build Settings.");
    }

    // ------------------------------------------------------------ preview ---

    /// <summary>
    /// Temporarily switches the dialogue canvas to camera-space with sample text
    /// visible so the offscreen render captures the full composition, then reverts.
    /// </summary>
    private static void TryCaptureComposition(Camera cam, Canvas canvas, CanvasGroup panel,
                                              TMP_Text body, TMP_Text name, string path)
    {
        RenderMode prevMode = canvas.renderMode;
        Camera prevCam = canvas.worldCamera;
        float prevDist = canvas.planeDistance;
        float prevAlpha = panel != null ? panel.alpha : 1f;
        string prevBody = body != null ? body.text : "";
        string prevName = name != null ? name.text : "";
        int prevVis = body != null ? body.maxVisibleCharacters : 0;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = Mathf.Max(cam.nearClipPlane * 2f, 2f);
            if (panel != null) panel.alpha = 1f;
            if (name != null) name.text = "Moema";
            if (body != null)
            {
                body.text = "Olá! Eu sou a Moema. Bem-vindos ao nosso podcast.";
                body.ForceMeshUpdate();
                body.maxVisibleCharacters = body.textInfo.characterCount;
            }
            Canvas.ForceUpdateCanvases();
            TryCapturePreview(cam, path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MoemaBuilder] composition capture skipped: " + e.Message);
        }
        finally
        {
            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCam;
            canvas.planeDistance = prevDist;
            if (panel != null) panel.alpha = prevAlpha;
            if (body != null) { body.text = prevBody; body.maxVisibleCharacters = prevVis; }
            if (name != null) name.text = prevName;
        }
    }

    private static void TryCapturePreview(Camera cam, string path)
    {
        try
        {
            const int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            RenderTexture prevTarget = cam.targetTexture;
            cam.targetTexture = rt;

            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (GraphicsSettings.currentRenderPipeline != null &&
                RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            cam.targetTexture = prevTarget;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Debug.Log("[MoemaBuilder] preview PNG written to " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MoemaBuilder] preview capture skipped: " + e.Message);
        }
    }
}
