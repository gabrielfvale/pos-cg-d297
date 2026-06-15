using UnityEngine;

/// <summary>
/// Emotion Fidelity Display — horizontal bar chart of the 7 FACS emotions.
/// Click a bar to highlight it and show the exact fidelity score tooltip.
/// Mean line shown as dashed white bar.
/// </summary>
public class EmotionFidelityDisplay : MonoBehaviour
{
    [Header("Parts (auto-populated)")]
    public GameObject[]  barObjects;        // 7 bar quads (children)
    public TextMesh[]    barLabels;         // emotion name labels
    public TextMesh      tooltipLabel;
    public GameObject    meanLineObject;    // dashed line object
    public TextMesh      titleLabel;

    [Header("Config")]
    public Color   baseBarColor     = new Color(0f, 1f, 0.53f);   // #00FF88
    public Color   selectedColor    = new Color(0.4f, 1f, 0.7f);
    public Color   dimColor         = new Color(0f, 0.4f, 0.2f);
    public float   maxBarWidth      = 2.8f;   // width at score 5.0
    public float   maxScore         = 5.0f;

    static readonly string[] EmotionNames = {
        "Anger", "Happiness", "Surprise", "Sadness", "Disgust", "Fear", "Contempt"
    };
    static readonly float[] Scores = {
        3.87f, 3.41f, 3.34f, 2.89f, 2.58f, 2.52f, 2.29f
    };
    const float MeanScore = 2.99f;

    int _selected = -1;
    Renderer[] _barRenderers;
    Material[] _barMats;
    Vector3[]  _baseScales;

    void Start()
    {
        _barRenderers = new Renderer[barObjects.Length];
        _barMats      = new Material[barObjects.Length];
        _baseScales   = new Vector3[barObjects.Length];

        for (int i = 0; i < barObjects.Length; i++)
        {
            _barRenderers[i] = barObjects[i].GetComponent<Renderer>();
            if (_barRenderers[i]) _barMats[i] = _barRenderers[i].material;

            float widthRatio = Scores[i] / maxScore;
            Vector3 s = barObjects[i].transform.localScale;
            s.x = maxBarWidth * widthRatio;
            barObjects[i].transform.localScale = s;
            _baseScales[i] = barObjects[i].transform.localScale;

            // align bar to left edge
            Vector3 p = barObjects[i].transform.localPosition;
            p.x = -maxBarWidth / 2f + s.x / 2f;
            barObjects[i].transform.localPosition = p;
            _baseScales[i] = barObjects[i].transform.localScale;

            SetBarColor(i, baseBarColor);
            if (barLabels != null && i < barLabels.Length)
                barLabels[i].text = EmotionNames[i];
        }

        if (titleLabel) titleLabel.text = "FIDELITY ASSESSMENT\nLikert-5 Scale";
        if (tooltipLabel) tooltipLabel.gameObject.SetActive(false);
        SetupMeanLine();
    }

    void SetupMeanLine()
    {
        if (!meanLineObject) return;
        Vector3 p = meanLineObject.transform.localPosition;
        p.x = -maxBarWidth / 2f + maxBarWidth * (MeanScore / maxScore);
        meanLineObject.transform.localPosition = p;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                for (int i = 0; i < barObjects.Length; i++)
                {
                    if (hit.collider && hit.collider.gameObject == barObjects[i])
                    {
                        SelectBar(i);
                        return;
                    }
                }
                Deselect();
            }
        }

        // Glow pulse on selected
        if (_selected >= 0 && _selected < barObjects.Length)
        {
            float e = 0.6f + Mathf.Sin(Time.time * 4f) * 0.4f;
            if (_barMats[_selected])
                _barMats[_selected].SetColor("_EmissionColor", baseBarColor * e);
        }
    }

    void SelectBar(int i)
    {
        _selected = i;
        for (int j = 0; j < barObjects.Length; j++)
            SetBarColor(j, j == i ? selectedColor : dimColor);

        if (tooltipLabel)
        {
            tooltipLabel.gameObject.SetActive(true);
            tooltipLabel.text = $"{EmotionNames[i]}\nScore: {Scores[i]:0.00} / 5.00\n" +
                                 "Rated by 3 game-experienced evaluators";
            tooltipLabel.transform.position =
                barObjects[i].transform.position + Vector3.up * 0.4f + Vector3.forward * -0.1f;
        }
    }

    void Deselect()
    {
        _selected = -1;
        for (int i = 0; i < barObjects.Length; i++) SetBarColor(i, baseBarColor);
        if (tooltipLabel) tooltipLabel.gameObject.SetActive(false);
    }

    void SetBarColor(int i, Color c)
    {
        if (_barMats[i] == null) return;
        _barMats[i].color = c;
        _barMats[i].SetColor("_EmissionColor", c * 0.5f);
        _barMats[i].EnableKeyword("_EMISSION");
    }
}
