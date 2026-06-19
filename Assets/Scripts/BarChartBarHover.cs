using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Detecta hover via Physics.Raycast (World Space Canvas) — espelho do TableRowHover.
/// Quando hovered, avança a barra em Z, muda sua cor de destaque e troca o sprite
/// do objeto de imagem alvo na cena (igual ao TableRowHover).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class BarChartBarHover : MonoBehaviour
{
    [Header("Hover Z")]
    public float hoverOffsetZ = -1.5f;
    public float animSpeed    = 10f;

    [Header("Cores")]
    public Color hoverColor  = new Color(0.98f, 0.76f, 0.18f, 1f);
    public Color dimmedColor = new Color(0.25f, 0.50f, 0.80f, 0.30f);

    [Header("Dados")]
    public int barIndex = 0;

    [Header("Troca de Imagem")]
    public string   imageTargetName = "ExemplosExpressoesGeradas";
    public Sprite[] rowSprites      = new Sprite[0];

    // componentes internos
    private BoxCollider _col;
    private Image       _barImage;
    private Color       _origColor;

    // estado
    private Vector3 _originLocal;
    private Vector3 _targetLocal;
    private bool    _hovered;

    // referência ao builder pai
    private BarChartBuilder _builder;

    // referência cacheada ao objeto de imagem na cena (igual ao TableRowHover)
    private Image  _targetImage;

    void Awake()
    {
        _col      = GetComponent<BoxCollider>();
        _barImage = GetComponent<Image>();
        if (_barImage == null) _barImage = GetComponentInChildren<Image>(true);
        if (_barImage != null) _origColor = _barImage.color;

        _builder = GetComponentInParent<BarChartBuilder>();
    }

    void Start()
    {
        _originLocal = transform.localPosition;
        _targetLocal = _originLocal;

        if (string.IsNullOrEmpty(imageTargetName)) return;

        // Busca o objeto de imagem na cena pelo nome — idêntico ao TableRowHover
        GameObject targetGO = GameObject.Find(imageTargetName);
        if (targetGO == null)
        {
            Debug.LogWarning($"[BarChartBarHover] GameObject '{imageTargetName}' não encontrado.");
            return;
        }

        _targetImage = targetGO.GetComponent<Image>();
        if (_targetImage == null)
            _targetImage = targetGO.GetComponentInChildren<Image>(true);

        if (_targetImage == null)
        {
            Debug.LogWarning($"[BarChartBarHover] GameObject '{imageTargetName}' não tem Image.");
            return;
        }

        // Carrega sprite via Resources se não foi atribuído — idêntico ao TableRowHover
        if (rowSprites == null || rowSprites.Length <= barIndex || rowSprites[barIndex] == null)
        {
            string spriteName = $"i{barIndex + 1}";
            Sprite loaded     = Resources.Load<Sprite>(spriteName);

            if (loaded != null)
            {
                if (rowSprites == null || rowSprites.Length <= barIndex)
                {
                    Sprite[] expanded = new Sprite[barIndex + 1];
                    if (rowSprites != null) rowSprites.CopyTo(expanded, 0);
                    rowSprites = expanded;
                }
                rowSprites[barIndex] = loaded;
                Debug.Log($"[BarChartBarHover] Sprite '{spriteName}' carregado para barra {barIndex + 1}.");
            }
            else
            {
                Debug.LogWarning($"[BarChartBarHover] Sprite '{spriteName}' não encontrado em Resources/.");
            }
        }
    }

    void Update()
    {
        if (Camera.main == null) return;

        Ray  ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool hit = _col.Raycast(ray, out _, 500f);

        if (hit  && !_hovered) EnterHover();
        if (!hit &&  _hovered) ExitHover();

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            _targetLocal,
            Time.deltaTime * animSpeed);
    }

    public void SetOrigin(Vector3 localPos)
    {
        _originLocal = localPos;
        _targetLocal = localPos;
        transform.localPosition = localPos;
        ResizeCollider();
    }

    /// <summary>Chamado pelo builder para aplicar highlight/dim externamente.</summary>
    public void ApplyHighlight(bool isHovered, bool anyHovered)
    {
        if (_barImage == null) return;

        if (!anyHovered)
            _barImage.color = _origColor;
        else if (isHovered)
            _barImage.color = hoverColor;
        else
            _barImage.color = dimmedColor;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private void EnterHover()
    {
        _hovered     = true;
        _targetLocal = _originLocal + new Vector3(0f, 0f, hoverOffsetZ);

        // Troca sprite — idêntico ao TableRowHover.EnterHover()
        if (_targetImage != null &&
            rowSprites != null && barIndex < rowSprites.Length &&
            rowSprites[barIndex] != null)
        {
            _targetImage.sprite = rowSprites[barIndex];
        }

        _builder?.OnBarEnterHover(barIndex);
    }

    private void ExitHover()
    {
        _hovered     = false;
        _targetLocal = _originLocal;

        // Sprite mantido intencionalmente — igual ao TableRowHover.ExitHover()
        _builder?.OnBarExitHover(barIndex);
    }

    private void ResizeCollider()
    {
        if (_col == null || _barImage == null) return;

        var rt = _barImage.GetComponent<RectTransform>();
        if (rt == null) return;

        _col.size      = new Vector3(rt.sizeDelta.x, rt.sizeDelta.y, 0.1f);
        _col.center    = new Vector3(rt.sizeDelta.x * 0.5f, rt.sizeDelta.y * 0.5f, 0f);
        _col.isTrigger = true;
    }
}
