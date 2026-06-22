using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drives a visual-novel style bottom dialogue box for the "Moema podcast" scene.
///
/// The Moema character voices both sides of the conversation (Moema and Moemo).
/// As the podcast audio plays, this component looks up the line whose start time
/// has been reached and shows the speaker name (accent-coloured) and the caption
/// text, with an optional typewriter reveal.
///
/// The line list is fully editable in the Inspector: replace the placeholder
/// lines with the real transcript (speaker, text and the start time in seconds).
/// </summary>
[DisallowMultipleComponent]
public class MoemaDialogueController : MonoBehaviour
{
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("Who is speaking. Must match Speaker A or Speaker B to pick the accent colour.")]
        public string speaker = "Moema";

        [TextArea(1, 4)]
        public string text = "";

        [Tooltip("Seconds from the start of the audio when this line should appear.")]
        public float time = 0f;
    }

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("UI references")]
    public CanvasGroup dialoguePanel;
    public TMP_Text nameplateText;
    public TMP_Text bodyText;
    public Image nameplateBackground;

    [Header("Speakers")]
    public string speakerA = "Moema";
    public Color colorA = new Color(0.96f, 0.55f, 0.30f, 1f);
    public string speakerB = "Moemo";
    public Color colorB = new Color(0.36f, 0.70f, 0.96f, 1f);

    [Header("Dialogue (edit to match the audio; sorted by time automatically)")]
    public List<DialogueLine> lines = new List<DialogueLine>();

    [Header("Typewriter")]
    public bool typewriter = true;
    public float charsPerSecond = 38f;

    [Header("Controls")]
    [Tooltip("Press R to restart the podcast from the beginning.")]
    public bool allowRestart = true;

    private int currentIndex = -1;
    private float typeProgress = 0f;

    private void Reset()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        // Keep the list ordered so the time lookup is a simple scan.
        lines.Sort((a, b) => a.time.CompareTo(b.time));
    }

    private void Start()
    {
        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
            audioSource.Play();

        ApplyLine(-1);
    }

    private void Update()
    {
        HandleInput();

        float t = (audioSource != null && audioSource.clip != null)
            ? audioSource.time
            : Time.timeSinceLevelLoad;

        int idx = IndexForTime(t);
        if (idx != currentIndex)
            ApplyLine(idx);
        else
            AdvanceTypewriter();
    }

    private int IndexForTime(float t)
    {
        int idx = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].time <= t) idx = i;
            else break;
        }
        return idx;
    }

    private void ApplyLine(int idx)
    {
        currentIndex = idx;
        typeProgress = 0f;

        if (idx < 0 || idx >= lines.Count)
        {
            if (dialoguePanel != null) dialoguePanel.alpha = 0f;
            return;
        }

        if (dialoguePanel != null) dialoguePanel.alpha = 1f;

        DialogueLine line = lines[idx];
        Color accent = ColorForSpeaker(line.speaker);

        if (nameplateText != null)
        {
            nameplateText.text = line.speaker;
            nameplateText.color = Color.white;
        }
        if (nameplateBackground != null)
            nameplateBackground.color = accent;

        if (bodyText != null)
        {
            bodyText.text = line.text;
            bodyText.ForceMeshUpdate();
            bodyText.maxVisibleCharacters = typewriter ? 0 : bodyText.textInfo.characterCount;
        }
    }

    private void AdvanceTypewriter()
    {
        if (!typewriter || bodyText == null || currentIndex < 0) return;

        int total = bodyText.textInfo.characterCount;
        if (bodyText.maxVisibleCharacters >= total) return;

        typeProgress += Time.deltaTime * Mathf.Max(1f, charsPerSecond);
        bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(typeProgress));
    }

    private Color ColorForSpeaker(string speaker)
    {
        if (!string.IsNullOrEmpty(speaker) &&
            speaker.Equals(speakerB, System.StringComparison.OrdinalIgnoreCase))
            return colorB;
        return colorA;
    }

    private void HandleInput()
    {
        if (!allowRestart || audioSource == null) return;

        if (RestartPressed())
        {
            audioSource.Stop();
            audioSource.time = 0f;
            audioSource.Play();
            currentIndex = -99; // force a refresh on the next frame
        }
    }

    private bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && kb.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }
}
