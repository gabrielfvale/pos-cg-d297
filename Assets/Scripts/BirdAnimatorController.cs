using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BirdAnimator))]
public class BirdAnimatorController : MonoBehaviour
{
    // ─── Tipos ────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class KeywordTrigger
    {
        [Tooltip("Nome exato da BonusAnimation no BirdAnimator que será forçada")]
        public string bonusAnimationName;

        [Tooltip("Palavras-chave que ativam essa animação (qualquer uma basta)")]
        public List<string> keywords = new List<string>();
    }

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Audio")]
    public AudioSource podcastAudio;

    [Header("SRT")]
    [Tooltip("Nome do arquivo em Resources/ (sem extensão)")]
    public string srtFileName = "podcast_legendas";
    [Tooltip("Nome do locutor que este pássaro representa")]
    public string speakerName = "Speaker 1";
    [Tooltip("Atribuir entradas sem locutor identificado a este pássaro")]
    public bool claimUnknownSpeaker = false;

    [Header("Tolerância")]
    [Tooltip("Antecipa o Talk X segundos antes do início")]
    public float startOffset = -0.05f;
    [Tooltip("Mantém o Talk X segundos após o fim")]
    public float endOffset   = 0.1f;

    [Header("Gatilhos por Palavra-chave")]
    [Tooltip("Cada entrada associa palavras-chave a uma animação especial do Talk")]
    public List<KeywordTrigger> keywordTriggers = new List<KeywordTrigger>();

    [Header("Debug")]
    public bool logStateChanges  = false;
    public bool logKeywordHits   = false;

    // ─── Privado ──────────────────────────────────────────────────────────────

    private BirdAnimator             _bird;
    private List<SRTParser.SRTEntry> _myEntries   = new List<SRTParser.SRTEntry>();
    private bool                     _isTalking;

    // Rastreia qual entrada SRT está ativa para não re-disparar
    private int  _lastTriggeredEntryIndex = -1;

    // ─── Unity ────────────────────────────────────────────────────────────────

    void Awake()  => _bird = GetComponent<BirdAnimator>();

    void Start()
    {
        LoadSRT();
        _bird.Idle();
    }

    void Update()
    {
        if (podcastAudio == null || !podcastAudio.isPlaying) return;

        float t = podcastAudio.time;

        // Encontra entrada ativa (se houver)
        SRTParser.SRTEntry activeEntry = GetActiveEntry(t);
        bool shouldTalk = activeEntry != null;

        // ── Transição de estado ───────────────────────────────────────────────
        if (shouldTalk && !_isTalking)  EnterTalk();
        if (!shouldTalk && _isTalking)  EnterIdle();

        // ── Gatilho de palavra-chave ──────────────────────────────────────────
        if (activeEntry != null && activeEntry.index != _lastTriggeredEntryIndex)
        {
            _lastTriggeredEntryIndex = activeEntry.index;
            CheckKeywordTriggers(activeEntry.text);
        }
    }

    // ─── Controle manual ─────────────────────────────────────────────────────

    public void ForceTalk() => EnterTalk();
    public void ForceIdle() => EnterIdle();

    public void ReloadSRT(string newFileName = null)
    {
        if (newFileName != null) srtFileName = newFileName;
        LoadSRT();
    }

    // ─── SRT ─────────────────────────────────────────────────────────────────

    void LoadSRT()
    {
        _myEntries.Clear();
        _lastTriggeredEntryIndex = -1;

        var asset = Resources.Load<TextAsset>(srtFileName);
        if (asset == null)
        {
            Debug.LogWarning($"[BirdController] Arquivo não encontrado: Resources/{srtFileName}");
            return;
        }

        var all = new SRTParser().Parse(asset.text);

        foreach (var entry in all)
        {
            bool isMe = string.Equals(entry.speaker, speakerName,
                            System.StringComparison.OrdinalIgnoreCase);
            bool isUnknown = claimUnknownSpeaker &&
                             string.Equals(entry.speaker, "Unknown",
                                 System.StringComparison.OrdinalIgnoreCase);

            if (isMe || isUnknown)
                _myEntries.Add(entry);
        }

        if (logStateChanges)
            Debug.Log($"[BirdController] '{speakerName}' – {_myEntries.Count} entradas de {all.Count} total.");
    }

    // ─── Intervalo ────────────────────────────────────────────────────────────

    SRTParser.SRTEntry GetActiveEntry(float t)
    {
        foreach (var e in _myEntries)
            if (t >= e.startTime + startOffset && t <= e.endTime + endOffset)
                return e;
        return null;
    }

    // ─── Palavras-chave ───────────────────────────────────────────────────────

    void CheckKeywordTriggers(string text)
    {
        if (keywordTriggers == null || keywordTriggers.Count == 0) return;

        foreach (var trigger in keywordTriggers)
        {
            if (string.IsNullOrEmpty(trigger.bonusAnimationName)) continue;
            if (trigger.keywords == null || trigger.keywords.Count == 0) continue;

            foreach (var kw in trigger.keywords)
            {
                if (string.IsNullOrEmpty(kw)) continue;

                if (text.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (logKeywordHits)
                        Debug.Log($"[BirdController] Keyword '{kw}' → '{trigger.bonusAnimationName}'");

                    _bird.ForceBonusAnimation(trigger.bonusAnimationName);
                    break; // uma keyword basta por trigger
                }
            }
        }
    }

    // ─── Estado ───────────────────────────────────────────────────────────────

    void EnterTalk()
    {
        _isTalking = true;
        _bird.Talk();
        if (logStateChanges)
            Debug.Log($"[BirdController] '{speakerName}' TALK @ {podcastAudio?.time:F2}s");
    }

    void EnterIdle()
    {
        _isTalking = false;
        _bird.Idle();
        if (logStateChanges)
            Debug.Log($"[BirdController] '{speakerName}' IDLE @ {podcastAudio?.time:F2}s");
    }
}