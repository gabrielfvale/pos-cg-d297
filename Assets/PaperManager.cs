using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class PaperManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public GameObject panelPaperList;

    private string selectedPDFPath = "";

    public void OnChoosePaperClicked()
    {
        // Abre o explorador de arquivos
        string path = OpenFileDialog();

        if (!string.IsNullOrEmpty(path))
        {
            selectedPDFPath = path;
            string fileName = Path.GetFileNameWithoutExtension(path);
            titleText.text = fileName;
            Debug.Log("PDF selecionado: " + path);
        }
    }

    private string OpenFileDialog()
    {
        #if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Escolha um PDF", "", "pdf");
        #else
        return "";
        #endif
    }

    public string GetSelectedPDFPath()
    {
        return selectedPDFPath;
    }
}
