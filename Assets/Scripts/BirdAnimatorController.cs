using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sincroniza as animações do pássaro com um AudioSource de podcast,
/// lendo os intervalos de fala diretamente de um arquivo .srt.
/// 
/// SETUP:
/// 1. Coloque o arquivo .srt na pasta Resources/ do projeto
/// 2. Atribua o AudioSource do podcast
/// 3. Configure o nome do locutor que este pássaro representa
/// 4. Dê Play — os intervalos são carregados automaticamente
/// </summary>
[RequireComponent(typeof(BirdAnimator))]
public class BirdAnimatorController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Audio")]
    [Tooltip("AudioSource do podcast")]
    public AudioSource podcastAudio;

    [Header("SRT")]
    [Tooltip("Arquivo .srt em Resources/ (sem extensão). Ex: 'podcast_legendas'")]
    public string srtFileName = "podcast_legendas";

    [Tooltip("Nome do locutor no .srt que este pássaro representa.\nEx: 'Speaker 1', 'Locutor A'")]
    public string speakerName = "Speaker 1";

    [Tooltip("Se verdadeiro, qualquer entrada sem locutor identificado é atribuída a este pássaro")]
    public bool claimUnknownSpeaker = false;

    [Header("Tolerância")]
    [Tooltip("Antecipa o Talk X segundos antes do início da fala (compensa latência)")]
    public float startOffset = -0.05f;
    [Tooltip("Mantém o Talk X segundos após o fim da fala")]
    public float endOffset   = 0.1f;

    [Header("Debug")]
    public bool logStateChanges = false;

    // ─── Privado ──────────────────────────────────────────────────────────────

    private BirdAnimator              _bird;
    private List<SRTParser.SRTEntry>  _myEntries = new List<SRTParser.SRTEntry>();
    private bool                      _isTalking;

    // ─── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        _bird = GetComponent<BirdAnimator>();
    }

    void Start()
    {
        LoadSRT();
        _bird.Idle();
    }

    void Update()
    {
        if (podcastAudio == null || !podcastAudio.isPlaying) return;

        float t = podcastAudio.time;
        bool shouldTalk = IsInsideMyInterval(t);

        if (shouldTalk && !_isTalking)  EnterTalk();
        if (!shouldTalk && _isTalking)  EnterIdle();
    }

    // ─── Controle manual ─────────────────────────────────────────────────────

    public void ForceTalk() => EnterTalk();
    public void ForceIdle() => EnterIdle();

    /// <summary>Recarrega o .srt em runtime (útil para troca de episódio).</summary>
    public void ReloadSRT(string newFileName = null)
    {
        if (newFileName != null) srtFileName = newFileName;
        LoadSRT();
    }

    // ─── SRT ──────────────────────────────────────────────────────────────────

    void LoadSRT()
    {
        _myEntries.Clear();

        var asset = Resources.Load<TextAsset>(srtFileName);
        if (asset == null)
        {
            Debug.LogWarning($"[BirdController] .srt não encontrado em Resources/{srtFileName}");
            return;
        }

        var all = new SRTParser().Parse(asset.text);

        foreach (var entry in all)
        {
            bool isMe = string.Equals(entry.speaker, speakerName,
                            System.StringComparison.OrdinalIgnoreCase);
            bool isUnknown = claimUnknownSpeaker &&
                             string.Equals(entry.speaker, "Speaker 1",
                                 System.StringComparison.OrdinalIgnoreCase);

            if (isMe || isUnknown)
                _myEntries.Add(entry);
        }

        if (logStateChanges)
            Debug.Log($"[BirdController] '{speakerName}' – {_myEntries.Count} entradas carregadas de {all.Count} total.");
    }

    // ─── Lógica de intervalo ──────────────────────────────────────────────────

    bool IsInsideMyInterval(float t)
    {
        foreach (var e in _myEntries)
            if (t >= e.startTime + startOffset && t <= e.endTime + endOffset)
                return true;
        return false;
    }

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