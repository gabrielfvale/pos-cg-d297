using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Constrói uma tabela UI dinamicamente.
/// Quando useStaticPaperData = true, cada linha ao ser hovered
/// troca o sprite do objeto de imagem alvo na cena.
/// </summary>
public class TableBuilder : MonoBehaviour
{
    [Header("Configuração Visual")]
    public Color headerColor     = new Color(0.15f, 0.35f, 0.60f, 1f);
    public Color rowColorA       = new Color(0.95f, 0.95f, 0.95f, 1f);
    public Color rowColorB       = new Color(0.85f, 0.88f, 0.92f, 1f);
    public Color borderColor     = new Color(0.40f, 0.40f, 0.40f, 1f);
    public Color headerTextColor = Color.white;
    public Color cellTextColor   = new Color(0.10f, 0.10f, 0.10f, 1f);
    public float cellWidth       = 10f;
    public float cellHeight      = 6f;
    public float fontSize        = 4.5f;
    public float headerFontSize  = 5f;

    [Header("Fonte dos Dados")]
    public bool useStaticPaperData = true;

    [Header("Troca de Imagem por Hover (requer useStaticPaperData = true)")]
    [Tooltip("Nome exato do GameObject de Imagem na cena que será controlado pelo hover")]
    public string imageTargetName = "ExemplosExpressoesGeradas";
    [Tooltip("Um sprite por linha da tabela — índice 0 = linha 1, etc.")]
    public Sprite[] rowSprites = new Sprite[7];

    public void Build(int rows, int columns, List<string> headers, List<List<string>> rowData)
    {
        if (useStaticPaperData)
        {
            headers = new List<string>
            {
                "Character Name",
                "Author",
                "Facial Blend Shapes",
                "Source"
            };

            rowData = new List<List<string>>
            {
                new() { "Alina",        "Jungle Jim",                         "174", "Sketchfab" },
                new() { "Asuna",        "MSGDI",                               "52", "Unity Asset Store" },
                new() { "Atticus (G2)", "Daz Originals and gypsangel",        "146", "Daz Store" },
                new() { "Ja-Long (G2)", "Daz Originals and Fred Winkler Art", "146", "Daz Store" },
                new() { "Zaniyah (G2)", "Daz Originals et al.",               "140", "Daz Store" },
                new() { "Disa (G3)",    "Daz Originals and Freja",            "130", "Daz Store" },
                new() { "Khalan (G8)",  "Matari3D",                           "249", "Daz Store" }
            };

            rows    = rowData.Count;
            columns = headers.Count;
        }

        foreach (Transform child in transform)
            Destroy(child.gameObject);

        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        float totalWidth  = 30f;
        float totalHeight = 30f;
        rt.sizeDelta = new Vector2(totalWidth, totalHeight);

        int totalRows  = rows + 1;
        cellWidth      = totalWidth  / columns;
        cellHeight     = totalHeight / totalRows;
        headerFontSize = 5f;
        fontSize       = 4.5f;

        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = borderColor;

        // Header (sem hover)
        GameObject headerRow = CreateRowObject("Row_Header", 0, totalRows);
        for (int c = 0; c < columns; c++)
        {
            string headerText = (headers != null && c < headers.Count) ? headers[c] : $"Col {c + 1}";
            CreateCell(headerRow, c, headerText, headerColor, headerTextColor, true);
        }

        // Linhas de dados
        for (int r = 0; r < rows; r++)
        {
            Color rowColor = (r % 2 == 0) ? rowColorA : rowColorB;
            GameObject rowGO = CreateRowObject($"Row_{r + 1}", r + 1, totalRows);

            for (int c = 0; c < columns; c++)
            {
                string cellText = "";
                if (rowData != null && r < rowData.Count && rowData[r] != null && c < rowData[r].Count)
                    cellText = rowData[r][c];
                CreateCell(rowGO, c, cellText, rowColor, cellTextColor, false);
            }

            TableRowHover hover    = rowGO.AddComponent<TableRowHover>();
            hover.rowIndex         = r;
            hover.useImageSwap     = useStaticPaperData;
            hover.imageTargetName  = imageTargetName;
            hover.rowSprites       = rowSprites;
            hover.SetOrigin(rowGO.transform.localPosition);
        }
    }

    private GameObject CreateRowObject(string rowName, int rowIndex, int totalRows)
    {
        GameObject rowGO = new GameObject(rowName);
        rowGO.transform.SetParent(transform, false);

        RectTransform rowRt = rowGO.AddComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.zero;
        rowRt.pivot     = Vector2.zero;

        float totalHeight      = totalRows * cellHeight;
        float y                = totalHeight - (rowIndex + 1) * cellHeight;
        rowRt.anchoredPosition = new Vector2(0f, y);
        rowRt.sizeDelta        = Vector2.zero;

        return rowGO;
    }

    private void CreateCell(GameObject parent, int col, string content, Color bgColor, Color textColor, bool isHeader)
    {
        GameObject cell = new GameObject($"Cell_{col}");
        cell.transform.SetParent(parent.transform, false);

        RectTransform cellRt    = cell.AddComponent<RectTransform>();
        cellRt.anchorMin        = Vector2.zero;
        cellRt.anchorMax        = Vector2.zero;
        cellRt.pivot            = Vector2.zero;
        cellRt.anchoredPosition = new Vector2(col * cellWidth, 0f);
        cellRt.sizeDelta        = new Vector2(cellWidth, cellHeight);

        Image cellBg = cell.AddComponent<Image>();
        cellBg.color = bgColor;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(cell.transform, false);

        RectTransform textRt = textGO.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(0.5f, 0.5f);
        textRt.offsetMax = new Vector2(-0.5f, -0.5f);

        TMP_Text tmp         = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text             = content;
        tmp.color            = textColor;
        tmp.fontStyle        = isHeader ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.overflowMode     = TextOverflowModes.Ellipsis;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = 1f;
        tmp.fontSizeMax      = isHeader ? headerFontSize : fontSize;
    }
}
