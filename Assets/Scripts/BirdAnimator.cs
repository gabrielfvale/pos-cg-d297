using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BirdAnimator : MonoBehaviour
{
    // ─── Tipos ────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class BonusAnimation
    {
        public string name = "Bonus";
        public Sprite[] frames;
        [Range(0f, 1f)] public float chance = 0.1f;
        public float fps = 8f;
        public int repeatCount = 1;
    }

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Cabeça")]
    [Tooltip("GameObject da cabeça — desativado durante Talk e animações especiais")]
    public GameObject headObject;

    [Header("Frames – Idle")]
    public Sprite[] idleFrames;
    public float idleFPS = 2f;

    [Header("Frames – Talk")]
    public Sprite[] talkFrames;
    public float talkFPS = 8f;

    [Header("Transição")]
    public float transitionDuration = 0.12f;

    [Header("Animações Especiais – Idle")]
    public List<BonusAnimation> idleBonusAnimations = new List<BonusAnimation>();

    [Header("Animações Especiais – Talk")]
    public List<BonusAnimation> talkBonusAnimations = new List<BonusAnimation>();

    [Header("Debug")]
    [HideInInspector] public string debugCurrentAnim  = "";
    [HideInInspector] public int    debugCurrentFrame = 0;

    // ─── Privado ──────────────────────────────────────────────────────────────

    private SpriteRenderer _sr;
    private SpriteRenderer _srFade;
    private Coroutine      _currentAnim;
    private Coroutine      _forcedBonus;

    private enum AnimState { None, Idle, Talk }
    private AnimState _state = AnimState.None;

    // ─── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();

        var fadeGO = new GameObject("_FadeLayer");
        fadeGO.transform.SetParent(transform, false);
        _srFade = fadeGO.AddComponent<SpriteRenderer>();
        _srFade.sortingLayerID = _sr.sortingLayerID;
        _srFade.sortingOrder   = _sr.sortingOrder - 1;
        _srFade.color          = new Color(1, 1, 1, 0);
    }

    void Start() => SetHead(true);

    // ─── API pública ──────────────────────────────────────────────────────────

    public void Idle()
    {
        if (_state == AnimState.Idle) return;
        _state = AnimState.Idle;
        SwitchTo(IdleLoop());
    }

    public void Talk()
    {
        if (_state == AnimState.Talk) return;
        _state = AnimState.Talk;
        SwitchTo(TalkLoop());
    }

    public void StopAnimation()
    {
        _state = AnimState.None;
        StopAllCoroutines();
        if (idleFrames != null && idleFrames.Length > 0)
            _sr.sprite = idleFrames[0];
        debugCurrentAnim  = "Stopped";
        debugCurrentFrame = 0;
        SetHead(true);
    }

    /// <summary>
    /// Força uma animação especial pelo nome imediatamente,
    /// interrompendo o loop atual e retomando após terminar.
    /// </summary>
    public void ForceBonusAnimation(string bonusName)
    {
        // Busca nas duas listas
        BonusAnimation found = FindBonus(bonusName, talkBonusAnimations)
                            ?? FindBonus(bonusName, idleBonusAnimations);

        if (found == null)
        {
            Debug.LogWarning($"[BirdAnimator] BonusAnimation '{bonusName}' não encontrada.");
            return;
        }

        if (_forcedBonus != null) StopCoroutine(_forcedBonus);
        _forcedBonus = StartCoroutine(ForcedBonusRoutine(found));
    }

    // ─── Loops principais ─────────────────────────────────────────────────────

    private IEnumerator IdleLoop()
    {
        if (idleFrames == null || idleFrames.Length == 0) yield break;

        debugCurrentAnim = "Idle";
        SetHead(true);
        yield return CrossFadeTo(idleFrames[0]);

        float delay = 1f / Mathf.Max(idleFPS, 0.1f);
        int   i     = 0;

        while (true)
        {
            if (i == 0)
            {
                BonusAnimation bonus = PickBonus(idleBonusAnimations);
                if (bonus != null)
                {
                    SetHead(false);
                    yield return PlayBonus(bonus);
                    SetHead(true);
                    debugCurrentAnim = "Idle";
                    yield return CrossFadeTo(idleFrames[0]);
                    i = 0;
                    continue;
                }
            }

            SetFrame(idleFrames, i);
            i = (i + 1) % idleFrames.Length;
            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator TalkLoop()
    {
        if (talkFrames == null || talkFrames.Length == 0) yield break;

        debugCurrentAnim = "Talk";
        SetHead(false);
        yield return CrossFadeTo(talkFrames[0]);

        float delay = 1f / Mathf.Max(talkFPS, 0.1f);
        int   i     = 0;

        while (true)
        {
            if (i == 0)
            {
                BonusAnimation bonus = PickBonus(talkBonusAnimations);
                if (bonus != null)
                {
                    yield return PlayBonus(bonus);
                    debugCurrentAnim = "Talk";
                    yield return CrossFadeTo(talkFrames[0]);
                    i = 0;
                    continue;
                }
            }

            SetFrame(talkFrames, i);
            i = (i + 1) % talkFrames.Length;
            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator PlayBonus(BonusAnimation bonus)
    {
        if (bonus.frames == null || bonus.frames.Length == 0) yield break;

        debugCurrentAnim = $"Bonus: {bonus.name}";
        yield return CrossFadeTo(bonus.frames[0]);

        float delay = 1f / Mathf.Max(bonus.fps, 0.1f);

        for (int rep = 0; rep < Mathf.Max(bonus.repeatCount, 1); rep++)
            for (int i = 0; i < bonus.frames.Length; i++)
            {
                SetFrame(bonus.frames, i);
                yield return new WaitForSeconds(delay);
            }
    }

    /// <summary>
    /// Pausa o loop atual, toca a bonus forçada, depois retoma o estado anterior.
    /// </summary>
    private IEnumerator ForcedBonusRoutine(BonusAnimation bonus)
    {
        // Salva estado atual para retomar depois
        AnimState prevState = _state;

        // Pausa o loop principal sem mudar _state
        if (_currentAnim != null)
        {
            StopCoroutine(_currentAnim);
            _currentAnim = null;
        }

        SetHead(false);
        yield return PlayBonus(bonus);

        // Retoma estado anterior
        _state = AnimState.None; // força re-entrada
        if (prevState == AnimState.Talk) Talk();
        else                             Idle();

        _forcedBonus = null;
    }

    // ─── Cross-fade ───────────────────────────────────────────────────────────

    private IEnumerator CrossFadeTo(Sprite nextSprite)
    {
        if (transitionDuration <= 0f || nextSprite == null)
        {
            if (nextSprite != null) _sr.sprite = nextSprite;
            yield break;
        }

        _srFade.sprite = _sr.sprite;
        _srFade.color  = new Color(1, 1, 1, 1);
        _sr.sprite     = nextSprite;
        _sr.color      = new Color(1, 1, 1, 0);

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / transitionDuration);
            _sr.color     = new Color(1, 1, 1, t);
            _srFade.color = new Color(1, 1, 1, 1f - t);
            yield return null;
        }

        _sr.color     = Color.white;
        _srFade.color = new Color(1, 1, 1, 0);
    }

    // ─── Utilitários ──────────────────────────────────────────────────────────

    private void SetHead(bool active)
    {
        if (headObject != null) headObject.SetActive(active);
    }

    private void SetFrame(Sprite[] frames, int index)
    {
        if (frames[index] == null) return;
        _sr.sprite        = frames[index];
        debugCurrentFrame = index;
    }

    private BonusAnimation FindBonus(string bonusName, List<BonusAnimation> list)
    {
        if (list == null) return null;
        foreach (var b in list)
            if (string.Equals(b.name, bonusName, System.StringComparison.OrdinalIgnoreCase))
                return b;
        return null;
    }

    private BonusAnimation PickBonus(List<BonusAnimation> list)
    {
        if (list == null || list.Count == 0) return null;

        var shuffled = new List<BonusAnimation>(list);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        foreach (var b in shuffled)
            if (b.frames != null && b.frames.Length > 0 && Random.value < b.chance)
                return b;

        return null;
    }

    private void SwitchTo(IEnumerator routine)
    {
        if (_currentAnim != null) StopCoroutine(_currentAnim);
        _currentAnim = StartCoroutine(routine);
    }
}