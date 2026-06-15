using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pipeline Terminal — visualises the 5-node RAG pipeline from Figure 1.
/// Nodes pulse in sequence; arrows light up; particles flow between nodes.
/// Click a node to pause and show its tooltip.
/// </summary>
public class PipelineTerminal : MonoBehaviour
{
    [Header("Runtime (auto-populated)")]
    public GameObject[] pipelineNodes;   // 5 node quads
    public GameObject[] arrowObjects;    // 4 arrow objects between nodes
    public ParticleSystem[] dataFlows;   // 4 particle systems on arrows
    public TextMesh tooltipLabel;
    public TextMesh nodeLabelLLM;        // special label on last node

    [Header("Config")]
    public float nodeActiveDuration = 1.2f;
    public float arrowFlowDuration  = 0.6f;
    public Color baseColor          = new Color(1f, 0.72f, 0f);     // amber
    public Color activeColor        = new Color(1f, 0.95f, 0.3f);   // bright amber
    public Color arrowActiveColor   = new Color(1f, 0.85f, 0.1f);

    static readonly string[] NodeLabels = {
        "Unity Plugin", "RAG App", "OpenFace", "Redis DB", "LLM Animator"
    };
    static readonly string[] Tooltips = {
        "Captures NPC emotion intent and\nsends request to the RAG pipeline.",
        "Retrieves relevant blend-shape→AU\nmappings from the vector database.",
        "Detects Action Units from rendered\nblend-shape images via computer vision.",
        "Stores precomputed blend-shape→AU\nmappings for fast semantic retrieval.",
        "Receives AU targets and generates\nanimation keyframes for the NPC face."
    };

    int  _active    = -1;
    bool _paused    = false;
    bool _running   = false;
    Coroutine _loop;

    Renderer[]  _nodeRenderers;
    Renderer[]  _arrowRenderers;
    Material[]  _nodeMats;
    Material[]  _arrowMats;

    void Start()
    {
        CacheRenderers();
        SetAllBase();
        if (tooltipLabel) tooltipLabel.gameObject.SetActive(false);
        _loop = StartCoroutine(PipelineLoop());
    }

    void CacheRenderers()
    {
        _nodeRenderers  = new Renderer[pipelineNodes.Length];
        _nodeMats       = new Material[pipelineNodes.Length];
        for (int i = 0; i < pipelineNodes.Length; i++)
        {
            _nodeRenderers[i] = pipelineNodes[i].GetComponentInChildren<Renderer>();
            if (_nodeRenderers[i]) _nodeMats[i] = _nodeRenderers[i].material;
        }
        _arrowRenderers = new Renderer[arrowObjects.Length];
        _arrowMats      = new Material[arrowObjects.Length];
        for (int i = 0; i < arrowObjects.Length; i++)
        {
            _arrowRenderers[i] = arrowObjects[i].GetComponentInChildren<Renderer>();
            if (_arrowRenderers[i]) _arrowMats[i] = _arrowRenderers[i].material;
        }
    }

    void SetAllBase()
    {
        foreach (var m in _nodeMats)  if (m) { m.color = baseColor; m.SetColor("_EmissionColor", baseColor * 0.3f); }
        foreach (var m in _arrowMats) if (m) { m.color = baseColor * 0.4f; m.SetColor("_EmissionColor", Color.black); }
    }

    IEnumerator PipelineLoop()
    {
        _running = true;
        while (true)
        {
            for (int i = 0; i < pipelineNodes.Length; i++)
            {
                if (_paused) { yield return new WaitUntil(() => !_paused); }

                ActivateNode(i);
                if (i < arrowObjects.Length) ActivateArrow(i);
                yield return new WaitForSeconds(nodeActiveDuration);
                DeactivateNode(i);
                if (i < arrowObjects.Length) DeactivateArrow(i);
            }
            // brief pause before restart
            yield return new WaitForSeconds(0.8f);
        }
    }

    void ActivateNode(int i)
    {
        _active = i;
        if (_nodeMats[i])
        {
            _nodeMats[i].color = activeColor;
            _nodeMats[i].SetColor("_EmissionColor", activeColor * 1.2f);
            _nodeMats[i].EnableKeyword("_EMISSION");
        }
    }

    void DeactivateNode(int i)
    {
        if (_nodeMats[i])
        {
            _nodeMats[i].color = baseColor;
            _nodeMats[i].SetColor("_EmissionColor", baseColor * 0.4f);
        }
        if (_active == i) _active = -1;
    }

    void ActivateArrow(int i)
    {
        if (_arrowMats[i])
        {
            _arrowMats[i].color = arrowActiveColor;
            _arrowMats[i].SetColor("_EmissionColor", arrowActiveColor);
            _arrowMats[i].EnableKeyword("_EMISSION");
        }
        if (dataFlows != null && i < dataFlows.Length && dataFlows[i]) dataFlows[i].Play();
    }

    void DeactivateArrow(int i)
    {
        if (_arrowMats[i])
        {
            _arrowMats[i].color = baseColor * 0.4f;
            _arrowMats[i].SetColor("_EmissionColor", Color.black);
        }
        if (dataFlows != null && i < dataFlows.Length && dataFlows[i]) dataFlows[i].Stop();
    }

    // Called by interaction layer (click detection)
    public void OnNodeClicked(int nodeIndex)
    {
        if (_paused && _active == nodeIndex) { Resume(); return; }
        ShowTooltip(nodeIndex);
    }

    void ShowTooltip(int i)
    {
        _paused = true;
        if (tooltipLabel)
        {
            tooltipLabel.gameObject.SetActive(true);
            tooltipLabel.text = $"[{NodeLabels[i]}]\n{Tooltips[i]}";
            tooltipLabel.transform.position = pipelineNodes[i].transform.position + Vector3.up * 1.1f;
        }
    }

    void Resume()
    {
        _paused = false;
        if (tooltipLabel) tooltipLabel.gameObject.SetActive(false);
    }

    void Update()
    {
        // Simple raycasts for click detection
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                for (int i = 0; i < pipelineNodes.Length; i++)
                {
                    if (hit.collider && hit.collider.transform.IsChildOf(pipelineNodes[i].transform))
                    {
                        OnNodeClicked(i);
                        return;
                    }
                }
                // click elsewhere resumes
                if (_paused) Resume();
            }
        }

        // Pulse scale on active node
        if (_active >= 0 && _active < pipelineNodes.Length)
        {
            float s = 1f + Mathf.Sin(Time.time * 5f) * 0.04f;
            pipelineNodes[_active].transform.localScale = Vector3.one * s;
        }
    }
}
