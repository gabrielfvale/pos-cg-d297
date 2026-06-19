using System.Collections;
using UnityEngine;

/// <summary>
/// Limitation Shard — fractured crystal levitates with slight oscillation.
/// Three limitation bubbles orbit it. Clicking a bubble expands with detail.
/// </summary>
public class LimitationShard : MonoBehaviour
{
    [Header("Parts (auto-populated)")]
    public Transform    crystal;
    public GameObject[] bubbleObjects;    // 3 limitation text panels
    public TextMesh[]   bubbleTexts;
    public TextMesh[]   detailTexts;
    public Renderer     crystalRenderer;
    public Renderer[]   crackRenderers;   // emissive crack lines

    [Header("Config")]
    public float bobAmplitude = 0.12f;
    public float bobSpeed     = 1.3f;
    public float orbitRadius  = 1.8f;
    public float orbitSpeed   = 25f;
    public Color crystalColor = new Color(0.29f, 0.33f, 0.41f);
    public Color crackColor   = new Color(1f, 0.27f, 0.27f, 0.6f);

    static readonly string[] BubbleTitles = {
        "⏱  Latency",
        "?  Confidence",
        "⚠  LLM Drift"
    };
    static readonly string[] BubbleShort = {
        "RAG not suitable for\nreal-time animation",
        "Confidence threshold ≠\nfidelity correlation",
        "LLM selects blend shapes\nby name, not AU mapping"
    };
    static readonly string[] BubbleDetail = {
        "Average response: 8.2 s\nMax observed: 25.3 s\nImpractical for live gameplay.",
        "No statistically significant link\nfound between AU confidence score\nand perceptual animation quality.",
        "LLM sometimes ignores AU data\nand picks blend shapes based on\nsuggestive names (e.g. 'smile')."
    };

    int _expanded = -1;
    Coroutine _crackPulse;
    Material   _crystalMat;
    Material[] _crackMats;
    float      _baseY;

    void Start()
    {
        _baseY = crystal ? crystal.localPosition.y : 0f;

        _crystalMat = crystalRenderer ? crystalRenderer.material : null;
        if (_crystalMat)
        {
            _crystalMat.color = crystalColor;
            _crystalMat.SetColor("_EmissionColor", Color.black);
        }

        _crackMats = new Material[crackRenderers.Length];
        for (int i = 0; i < crackRenderers.Length; i++)
        {
            if (crackRenderers[i])
            {
                _crackMats[i] = crackRenderers[i].material;
                _crackMats[i].color = crackColor;
                _crackMats[i].SetColor("_EmissionColor", crackColor * 0.3f);
                _crackMats[i].EnableKeyword("_EMISSION");
            }
        }

        for (int i = 0; i < bubbleTexts.Length; i++)
        {
            if (bubbleTexts[i])   bubbleTexts[i].text   = BubbleTitles[i] + "\n" + BubbleShort[i];
            if (detailTexts[i])   detailTexts[i].text   = BubbleDetail[i];
            if (detailTexts[i])   detailTexts[i].gameObject.SetActive(false);
        }

        _crackPulse = StartCoroutine(PulseCracks());
    }

    void Update()
    {
        // Bob crystal
        if (crystal)
        {
            Vector3 lp = crystal.localPosition;
            lp.y = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            crystal.localPosition = lp;
            crystal.Rotate(Vector3.up, 8f * Time.deltaTime);
        }

        // Orbit bubbles
        float baseAngle = Time.time * orbitSpeed;
        for (int i = 0; i < bubbleObjects.Length; i++)
        {
            float angle = baseAngle + i * 120f;
            float rad   = angle * Mathf.Deg2Rad;
            float yOff  = Mathf.Sin(Time.time * bobSpeed + i * 1.5f) * 0.15f;
            Vector3 offset = new Vector3(Mathf.Cos(rad), yOff, Mathf.Sin(rad)) * orbitRadius;
            bubbleObjects[i].transform.position = transform.position + offset;
            bubbleObjects[i].transform.LookAt(Camera.main.transform);
        }

        // Click
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                for (int i = 0; i < bubbleObjects.Length; i++)
                {
                    if (hit.collider && hit.collider.transform.IsChildOf(bubbleObjects[i].transform))
                    {
                        ToggleBubble(i);
                        return;
                    }
                }
            }
        }
    }

    void ToggleBubble(int i)
    {
        if (_expanded == i)
        {
            if (detailTexts[i]) detailTexts[i].gameObject.SetActive(false);
            _expanded = -1;
            return;
        }
        if (_expanded >= 0 && _expanded < detailTexts.Length)
            if (detailTexts[_expanded]) detailTexts[_expanded].gameObject.SetActive(false);

        _expanded = i;
        if (detailTexts[i]) detailTexts[i].gameObject.SetActive(true);
    }

    IEnumerator PulseCracks()
    {
        while (true)
        {
            float t = Mathf.Abs(Mathf.Sin(Time.time * 0.7f));
            foreach (var m in _crackMats)
                if (m != null)
                    m.SetColor("_EmissionColor", crackColor * (0.1f + t * 0.5f));
            yield return null;
        }
    }
}
