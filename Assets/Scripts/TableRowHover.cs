using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Detecta hover via Physics.Raycast (World Space Canvas).
/// Quando useImageSwap = true, ao entrar em hover troca o sprite
/// do objeto de imagem alvo na cena pelo sprite do índice da linha.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TableRowHover : MonoBehaviour
{
    [Header("Hover Z")]
    public float hoverOffsetZ = -1.5f;
    public float animSpeed    = 10f;

    [Header("Highlight")]
    public Color hoverColor = new Color(0.98f, 0.92f, 0.50f, 1f);

    [Header("Troca de Imagem")]
    public bool     useImageSwap    = false;
    public int      rowIndex        = 0;
    public string   imageTargetName = "Imagem";
    public Sprite[] rowSprites      = new Sprite[0];

    // estado interno
    private Vector3     _originLocal;
    private Vector3     _targetLocal;
    private bool        _hovered;

    private List<Image> _cellImages = new();
    private List<Color> _origColors = new();

    private BoxCollider _col;

    // referência cacheada ao objeto de imagem na cena
    private Image _targetImage;
    private Sprite _originalSprite;

    void Awake()
    {
        _col = GetComponent<BoxCollider>();
        CacheImages();
    }

void Start()
    {
        _originLocal = transform.localPosition;
        _targetLocal = _originLocal;

        if (!useImageSwap || string.IsNullOrEmpty(imageTargetName)) return;

        // Busca o objeto de imagem na cena pelo nome
        GameObject targetGO = GameObject.Find(imageTargetName);
        if (targetGO == null)
        {
            Debug.LogWarning($"[TableRowHover] GameObject '{imageTargetName}' não encontrado na cena.");
            return;
        }

        _targetImage = targetGO.GetComponent<Image>();
        if (_targetImage == null)
            _targetImage = targetGO.GetComponentInChildren<Image>(true);

        if (_targetImage == null)
        {
            Debug.LogWarning($"[TableRowHover] GameObject '{imageTargetName}' não tem componente Image.");
            return;
        }

        _originalSprite = _targetImage.sprite;

        // Carrega sprite pelo nome 'i1', 'i2', 'i3'... a partir de Resources
        // rowIndex é 0-based, então rowIndex 0 = i1, rowIndex 1 = i2, etc.
        if (rowSprites == null || rowSprites.Length == 0 || rowSprites[rowIndex] == null)
        {
            string spriteName = $"i{rowIndex + 1}";
            Sprite loaded = Resources.Load<Sprite>(spriteName);

            if (loaded != null)
            {
                // Garante array suficiente e armazena
                if (rowSprites == null || rowSprites.Length <= rowIndex)
                {
                    Sprite[] expanded = new Sprite[rowIndex + 1];
                    if (rowSprites != null) rowSprites.CopyTo(expanded, 0);
                    rowSprites = expanded;
                }
                rowSprites[rowIndex] = loaded;
                Debug.Log($"[TableRowHover] Sprite '{spriteName}' carregado via Resources para linha {rowIndex + 1}.");
            }
            else
            {
                Debug.LogWarning($"[TableRowHover] Sprite '{spriteName}' não encontrado em Resources/. Verifique se a imagem está em Assets/Resources/{spriteName}.png");
            }
        }
    }

    void Update()
    {
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

    // ---- helpers ----

    private void EnterHover()
    {
        _hovered     = true;
        _targetLocal = _originLocal + new Vector3(0f, 0f, hoverOffsetZ);

        for (int i = 0; i < _cellImages.Count; i++)
            _cellImages[i].color = hoverColor;

        // Troca sprite somente se useImageSwap ativo e sprite disponível
        if (useImageSwap && _targetImage != null &&
            rowSprites != null && rowIndex < rowSprites.Length &&
            rowSprites[rowIndex] != null)
        {
            _targetImage.sprite = rowSprites[rowIndex];
        }
    }

private void ExitHover()
    {
        _hovered     = false;
        _targetLocal = _originLocal;

        for (int i = 0; i < _cellImages.Count; i++)
            _cellImages[i].color = _origColors[i];

        // Sprite mantido intencionalmente - so troca quando outra linha for hovered
    }

    private void CacheImages()
    {
        _cellImages.Clear();
        _origColors.Clear();
        foreach (Transform child in transform)
        {
            var img = child.GetComponent<Image>();
            if (img != null)
            {
                _cellImages.Add(img);
                _origColors.Add(img.color);
            }
        }
    }

    private void ResizeCollider()
    {
        if (_col == null) return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (Transform child in transform)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt == null) continue;

            Vector2 pos  = rt.anchoredPosition;
            Vector2 size = rt.sizeDelta;

            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x + size.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y + size.y);
        }

        if (minX == float.MaxValue) return;

        float w = maxX - minX;
        float h = maxY - minY;

        _col.size      = new Vector3(w, h, 0.1f);
        _col.center    = new Vector3(minX + w * 0.5f, minY + h * 0.5f, 0f);
        _col.isTrigger = true;
    }
}
