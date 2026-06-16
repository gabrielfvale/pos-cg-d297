using System.Collections;
using UnityEngine;

/// <summary>
/// Face Calibration Pod — holographic head rotates above pedestal.
/// Click panel to trigger AU scan animation: scan lines sweep over the face,
/// AU labels appear with confidence values.
/// </summary>
public class FaceCalibrationPod : MonoBehaviour
{
    [Header("Parts (auto-populated)")]
    public Transform holoHead;
    public GameObject scanPanel;
    public GameObject[] auLabelObjects;   // 3 AU label text meshes
    public GameObject   scanLineObject;   // thin quad that sweeps up
    public Renderer     headRenderer;
    public ParticleSystem scanParticles;

    [Header("Config")]
    public float rotationSpeed   = 25f;
    public float scanDuration    = 2.0f;
    public Color cyanColor       = new Color(0f, 0.83f, 1f);
    public Color cyanDim         = new Color(0f, 0.4f, 0.6f);

    static readonly string[] AUNames = {
        "AU4 — Brow Lowerer",
        "AU7 — Lid Tightener",
        "AU25 — Lips Part"
    };
    static readonly float[] AUScores = { 0.87f, 0.73f, 0.91f };

    bool _scanning = false;

    TextMesh[] _auTextMeshes;

    void Start()
    {
        _auTextMeshes = new TextMesh[auLabelObjects.Length];
        for (int i = 0; i < auLabelObjects.Length; i++)
        {
            _auTextMeshes[i] = auLabelObjects[i].GetComponentInChildren<TextMesh>();
            if (_auTextMeshes[i]) _auTextMeshes[i].text = AUNames[i] + "\nConf: --";
            auLabelObjects[i].SetActive(false);
        }
        if (scanLineObject) scanLineObject.SetActive(false);
        SetHeadEmission(cyanDim);
    }

    void Update()
    {
        if (holoHead) holoHead.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        if (!_scanning && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && scanPanel &&
                hit.collider && hit.collider.transform.IsChildOf(scanPanel.transform))
                StartCoroutine(RunScan());
        }
    }

    IEnumerator RunScan()
    {
        _scanning = true;

        // Reset AU labels
        foreach (var go in auLabelObjects) go.SetActive(false);

        // Sweep scan line
        if (scanLineObject)
        {
            scanLineObject.SetActive(true);
            Vector3 start = holoHead.position - Vector3.up * 1f;
            Vector3 end   = holoHead.position + Vector3.up * 1f;
            float elapsed = 0f;
            while (elapsed < scanDuration)
            {
                elapsed += Time.deltaTime;
                scanLineObject.transform.position = Vector3.Lerp(start, end, elapsed / scanDuration);
                yield return null;
            }
            scanLineObject.SetActive(false);
        }
        else yield return new WaitForSeconds(scanDuration);

        if (scanParticles) scanParticles.Play();
        SetHeadEmission(cyanColor);

        // Reveal AU labels one by one
        for (int i = 0; i < auLabelObjects.Length; i++)
        {
            auLabelObjects[i].SetActive(true);
            if (_auTextMeshes[i])
                _auTextMeshes[i].text = AUNames[i] + $"\nConf: {AUScores[i]:0.00}";
            yield return new WaitForSeconds(0.35f);
        }

        yield return new WaitForSeconds(2.5f);

        // Fade out and reset
        SetHeadEmission(cyanDim);
        foreach (var go in auLabelObjects) go.SetActive(false);
        _scanning = false;
    }

    void SetHeadEmission(Color c)
    {
        if (!headRenderer) return;
        headRenderer.material.SetColor("_EmissionColor", c);
        headRenderer.material.EnableKeyword("_EMISSION");
    }
}
