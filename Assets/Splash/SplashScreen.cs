using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Self-contained splash screen. Shows a centered logo, then performs a simple
/// cross-fade into a full-screen video, then loads the next scene.
///
/// The whole UI (camera, canvas, logo and video surfaces) is built at runtime so
/// the scene only needs this single component. The logo and video are loaded by
/// name from a Resources folder (Assets/Splash/Resources), which keeps the scene
/// free of fragile asset references.
/// </summary>
[DisallowMultipleComponent]
public class SplashScreen : MonoBehaviour
{
    [Header("Resources (in Assets/Splash/Resources, no extension)")]
    [SerializeField] private string logoResourceName = "unifor_logo";
    [SerializeField] private string videoResourceName = "unifor_splash";

    [Header("Scene to load after the splash")]
    [SerializeField] private string nextSceneName = "SampleScene";
    [SerializeField] private int nextSceneBuildIndex = 1;

    [Header("Timing (seconds)")]
    [SerializeField] private float fadeInDuration = 0.8f;
    [SerializeField] private float logoHoldDuration = 1.6f;
    [SerializeField] private float crossfadeDuration = 0.8f;
    [SerializeField] private float videoEndFadeDuration = 0.8f;
    [SerializeField] private float videoPrepareTimeout = 8f;

    [Header("Look & feel")]
    [SerializeField] private Color backgroundColor = Color.black;
    [Tooltip("Logo width as a fraction of the 1920px reference width.")]
    [SerializeField] private float logoWidthFraction = 0.55f;
    [SerializeField] private bool allowSkip = true;

    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private RectTransform canvasRect;
    private CanvasGroup logoGroup;
    private CanvasGroup videoGroup;
    private RawImage videoImage;
    private RectTransform videoRect;
    private VideoPlayer videoPlayer;
    private RenderTexture videoRT;
    private bool skipRequested;
    private bool sequenceFinished;

    private void Awake()
    {
        BuildUI();
        SetupVideoPlayer();
    }

    private void Start()
    {
        StartCoroutine(RunSequence());
    }

    private void Update()
    {
        if (allowSkip && !skipRequested && !sequenceFinished && AnySkipInput())
            skipRequested = true;
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        // On Linux only VP8/VP9 (.webm) decode; H.264/.mp4 reports "format not supported".
        Debug.LogError($"[SplashScreen] VideoPlayer error: {message}");
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.targetTexture = null;
        }
        if (videoRT != null)
        {
            videoRT.Release();
            Destroy(videoRT);
            videoRT = null;
        }
    }

    // ---------------------------------------------------------------- build ---

    private void BuildUI()
    {
        // Camera: clears the screen to the background colour and hosts the
        // AudioListener that the video's Direct audio output needs.
        var camGO = new GameObject("SplashCamera");
        camGO.transform.SetParent(transform, false);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.cullingMask = 0;
        cam.orthographic = true;
        cam.depth = -100;
        camGO.AddComponent<AudioListener>();

        // Screen-space overlay canvas drawn above everything else.
        var canvasGO = new GameObject("SplashCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasRect = canvasGO.GetComponent<RectTransform>();

        // Solid background covering the whole screen.
        var bg = CreateStretchChild("Background", canvasRect);
        var bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = backgroundColor;
        bgImage.raycastTarget = false;

        // Video layer (added before the logo so the logo sits on top of it).
        var videoLayer = CreateStretchChild("VideoLayer", canvasRect);
        videoGroup = videoLayer.gameObject.AddComponent<CanvasGroup>();
        videoGroup.alpha = 0f;
        videoGroup.interactable = false;
        videoGroup.blocksRaycasts = false;

        var videoGO = new GameObject("Video", typeof(RectTransform));
        videoRect = videoGO.GetComponent<RectTransform>();
        videoRect.SetParent(videoLayer, false);
        videoRect.anchorMin = videoRect.anchorMax = new Vector2(0.5f, 0.5f);
        videoRect.pivot = new Vector2(0.5f, 0.5f);
        videoImage = videoGO.AddComponent<RawImage>();
        videoImage.color = Color.white;
        videoImage.raycastTarget = false;

        // Logo layer on top.
        var logoLayer = CreateStretchChild("LogoLayer", canvasRect);
        logoGroup = logoLayer.gameObject.AddComponent<CanvasGroup>();
        logoGroup.alpha = 0f;
        logoGroup.interactable = false;
        logoGroup.blocksRaycasts = false;

        var logoGO = new GameObject("Logo", typeof(RectTransform));
        var logoRect = logoGO.GetComponent<RectTransform>();
        logoRect.SetParent(logoLayer, false);
        logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        var logoImage = logoGO.AddComponent<Image>();
        logoImage.raycastTarget = false;
        logoImage.preserveAspect = true;

        var logoSprite = Resources.Load<Sprite>(logoResourceName);
        if (logoSprite != null)
        {
            logoImage.sprite = logoSprite;
            float aspect = logoSprite.rect.height > 0f
                ? logoSprite.rect.width / logoSprite.rect.height
                : 1f;
            float w = ReferenceWidth * Mathf.Clamp01(logoWidthFraction);
            logoRect.sizeDelta = new Vector2(w, w / Mathf.Max(aspect, 0.0001f));
        }
        else
        {
            Debug.LogWarning($"[SplashScreen] Logo '{logoResourceName}' not found in a Resources folder.");
            logoRect.sizeDelta = new Vector2(ReferenceWidth * 0.5f, ReferenceHeight * 0.3f);
        }
    }

    private static RectTransform CreateStretchChild(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void SetupVideoPlayer()
    {
        var clip = Resources.Load<VideoClip>(videoResourceName);
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.errorReceived += OnVideoError;

        if (clip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = clip;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetDirectAudioVolume(0, 1f);
            videoPlayer.Prepare();
        }
        else
        {
            Debug.LogWarning($"[SplashScreen] Video '{videoResourceName}' not found in a Resources folder.");
        }
    }

    // ------------------------------------------------------------- sequence ---

    private IEnumerator RunSequence()
    {
        logoGroup.alpha = 0f;
        videoGroup.alpha = 0f;

        // Fade the logo up from black.
        yield return Fade(logoGroup, 0f, 1f, fadeInDuration);

        // Hold the logo (skippable).
        yield return WaitOrSkip(logoHoldDuration);

        bool playedVideo = false;
        if (!skipRequested && videoPlayer != null && videoPlayer.clip != null)
        {
            yield return WaitForPrepared();
            if (videoPlayer.isPrepared)
            {
                CreateVideoSurface();
                videoPlayer.Play();
                playedVideo = true;

                // The simple fade between logo and video.
                yield return CrossFade(logoGroup, videoGroup, crossfadeDuration);

                // Play through to the end (or until skipped).
                yield return WaitForVideoEndOrSkip();
            }
        }

        // Fade whatever is on screen out to black.
        CanvasGroup top = playedVideo ? videoGroup : logoGroup;
        yield return Fade(top, top.alpha, 0f, videoEndFadeDuration);

        sequenceFinished = true;
        if (videoPlayer != null)
            videoPlayer.Stop();

        LoadNextScene();
    }

    private void CreateVideoSurface()
    {
        int w = (int)videoPlayer.width;
        int h = (int)videoPlayer.height;
        if (w <= 0 || h <= 0)
        {
            w = (int)ReferenceWidth;
            h = (int)ReferenceHeight;
        }

        videoRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        videoRT.Create();
        videoPlayer.targetTexture = videoRT;
        videoImage.texture = videoRT;

        // Fit the video inside the screen, preserving aspect (letterboxed on black).
        Canvas.ForceUpdateCanvases();
        Vector2 area = canvasRect.rect.size;
        if (area.x <= 0f || area.y <= 0f)
            area = new Vector2(ReferenceWidth, ReferenceHeight);

        float videoAspect = (float)w / h;
        float areaAspect = area.x / area.y;
        Vector2 size = videoAspect > areaAspect
            ? new Vector2(area.x, area.x / videoAspect)
            : new Vector2(area.y * videoAspect, area.y);
        videoRect.sizeDelta = size;
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }
        if (nextSceneBuildIndex >= 0 && nextSceneBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneBuildIndex);
            return;
        }
        Debug.LogError($"[SplashScreen] Could not load the next scene (name='{nextSceneName}', " +
                       $"index={nextSceneBuildIndex}). Make sure it is added to Build Settings.");
    }

    // -------------------------------------------------------------- helpers ---

    private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (skipRequested)
                break;
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = to;
    }

    private IEnumerator CrossFade(CanvasGroup outGroup, CanvasGroup inGroup, float duration)
    {
        if (duration <= 0f)
        {
            if (outGroup != null) outGroup.alpha = 0f;
            if (inGroup != null) inGroup.alpha = 1f;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (skipRequested)
                break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (outGroup != null) outGroup.alpha = 1f - k;
            if (inGroup != null) inGroup.alpha = k;
            yield return null;
        }
        if (outGroup != null) outGroup.alpha = 0f;
        if (inGroup != null) inGroup.alpha = 1f;
    }

    private IEnumerator WaitOrSkip(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (skipRequested)
                yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForPrepared()
    {
        float t = 0f;
        while (!videoPlayer.isPrepared && t < videoPrepareTimeout)
        {
            if (skipRequested)
                yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!videoPlayer.isPrepared)
            Debug.LogWarning("[SplashScreen] Video was not ready in time; skipping the video.");
    }

    private IEnumerator WaitForVideoEndOrSkip()
    {
        bool ended = false;
        void OnEnd(VideoPlayer source) => ended = true;

        videoPlayer.loopPointReached += OnEnd;
        while (!ended && !skipRequested)
            yield return null;
        videoPlayer.loopPointReached -= OnEnd;
    }

    private bool AnySkipInput()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            return true;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            return true;
        var gamepad = Gamepad.current;
        if (gamepad != null && (gamepad.startButton.wasPressedThisFrame || gamepad.buttonSouth.wasPressedThisFrame))
            return true;
        return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.anyKeyDown || Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }
}
