using System.Collections;
using UnityEngine;

/// <summary>
/// FACS Codex — rotating holographic bust with 7 orbiting emotion chips.
/// Clicking a chip highlights the relevant AUs on the face wireframe.
/// </summary>
public class FACSCodex : MonoBehaviour
{
    [Header("Parts (auto-populated)")]
    public Transform    bust;
    public GameObject[] emotionChips;     // 7 hex chip objects
    public GameObject[] auHighlights;     // muscle highlight objects on bust
    public TextMesh     infoLabel;
    public TextMesh     titleLabel;

    [Header("Config")]
    public float rotationSpeed = 20f;
    public float chipOrbitRadius = 2.2f;
    public Color cyanBase   = new Color(0f, 0.83f, 1f);
    public Color cyanBright = new Color(0.5f, 1f, 1f);
    public Color chipBase   = new Color(0f, 0.5f, 0.7f);
    public Color chipActive = new Color(0f, 0.9f, 1f);

    static readonly string[] EmotionNames = {
        "Anger", "Happiness", "Surprise", "Sadness", "Disgust", "Fear", "Contempt"
    };
    // Which AU highlights to activate per emotion (indices into auHighlights)
    static readonly int[][] EmotionAUs = {
        new[]{0,1},   // Anger:    Brow Lowerer + Upper Lid Raiser
        new[]{2,3},   // Happiness: Cheek Raiser + Lip Corner Puller
        new[]{1,4},   // Surprise: Upper Lid Raiser + Jaw Drop
        new[]{0,5},   // Sadness:  Brow Lowerer + Lip Corner Depressor
        new[]{6,7},   // Disgust:  Upper Lip Raiser + Nose Wrinkler
        new[]{1,0},   // Fear:     Upper Lid Raiser + Brow Lowerer
        new[]{8,5}    // Contempt: Lip Corner Puller (unilateral) + Brow Lowerer
    };
    static readonly string[] AUDescriptions = {
        "Anger → AU4 Brow Lowerer + AU23 Lip Tightener",
        "Happiness → AU6 Cheek Raiser + AU12 Lip Corner Puller",
        "Surprise → AU5 Upper Lid Raiser + AU27 Mouth Stretch",
        "Sadness → AU1 Inner Brow Raise + AU15 Lip Corner Depressor",
        "Disgust → AU9 Nose Wrinkler + AU16 Lower Lip Depressor",
        "Fear → AU5 Upper Lid Raiser + AU20 Lip Stretcher",
        "Contempt → AU14 Dimpler (unilateral)"
    };

    int _selectedChip = -1;
    Material[] _chipMats;
    Material[] _auMats;
    Coroutine  _auAnim;

    void Start()
    {
        _chipMats = new Material[emotionChips.Length];
        for (int i = 0; i < emotionChips.Length; i++)
        {
            var r = emotionChips[i].GetComponentInChildren<Renderer>();
            if (r) _chipMats[i] = r.material;
            SetChipColor(i, chipBase);

            // Label chip
            var tm = emotionChips[i].GetComponentInChildren<TextMesh>();
            if (tm) tm.text = EmotionNames[i];
        }

        _auMats = new Material[auHighlights.Length];
        for (int i = 0; i < auHighlights.Length; i++)
        {
            var r = auHighlights[i].GetComponentInChildren<Renderer>();
            if (r) _auMats[i] = r.material;
            auHighlights[i].SetActive(false);
        }

        if (titleLabel) titleLabel.text = "Ekman's FACS\nShared vocabulary between OpenFace and LLM";
        if (infoLabel)  infoLabel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (bust) bust.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // Orbit chips
        float baseAngle = Time.time * 15f;
        for (int i = 0; i < emotionChips.Length; i++)
        {
            float angle = baseAngle + i * (360f / emotionChips.Length);
            float rad   = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * chipOrbitRadius;
            emotionChips[i].transform.position = transform.position + offset + Vector3.up * 0.3f;
            emotionChips[i].transform.LookAt(emotionChips[i].transform.position + Vector3.up * 0f +
                (emotionChips[i].transform.position - transform.position).normalized);
        }

        // Click detection
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                for (int i = 0; i < emotionChips.Length; i++)
                {
                    if (hit.collider && hit.collider.transform.IsChildOf(emotionChips[i].transform))
                    {
                        SelectEmotion(i);
                        return;
                    }
                }
            }
        }
    }

    void SelectEmotion(int i)
    {
        _selectedChip = i;
        for (int j = 0; j < emotionChips.Length; j++)
            SetChipColor(j, j == i ? chipActive : chipBase);

        foreach (var go in auHighlights) go.SetActive(false);

        int[] aus = EmotionAUs[i];
        foreach (int auIdx in aus)
            if (auIdx < auHighlights.Length)
                auHighlights[auIdx].SetActive(true);

        if (infoLabel)
        {
            infoLabel.gameObject.SetActive(true);
            infoLabel.text = AUDescriptions[i];
        }

        if (_auAnim != null) StopCoroutine(_auAnim);
        _auAnim = StartCoroutine(PulseAUs(aus));
    }

    IEnumerator PulseAUs(int[] aus)
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 3f;
            float e = 0.5f + Mathf.Sin(t) * 0.5f;
            foreach (int idx in aus)
                if (idx < _auMats.Length && _auMats[idx] != null)
                    _auMats[idx].SetColor("_EmissionColor", cyanBase * e);
            yield return null;
        }
    }

    void SetChipColor(int i, Color c)
    {
        if (_chipMats[i] == null) return;
        _chipMats[i].color = c;
        _chipMats[i].SetColor("_EmissionColor", c * 0.8f);
        _chipMats[i].EnableKeyword("_EMISSION");
    }
}
