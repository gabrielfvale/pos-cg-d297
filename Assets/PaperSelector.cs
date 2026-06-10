using UnityEngine;

public class PaperSelector : MonoBehaviour
{
    [Header("Panels")]
    public GameObject panelSelection;
    public GameObject panelArticleCategory;
    public GameObject panelList;

    [Header("Controllers")]
    public WheelController wheelController;

    private string selectedType = "";

    void Start()
    {
        panelSelection.SetActive(true);
        panelArticleCategory.SetActive(false);
        panelList.SetActive(false);
    }

    // Botões Thesis e Dissertation
    public void OnSelectThesisOrDissertation(string type)
    {
        selectedType = type;
        panelSelection.SetActive(false);
        panelArticleCategory.SetActive(false);
        panelList.SetActive(true);
        wheelController.LoadPapers(type.ToLower());
    }

    // Botão Paper
    public void OnSelectPaper()
    {
        selectedType = "paper";
        panelSelection.SetActive(false);
        panelArticleCategory.SetActive(true);
        panelList.SetActive(false);
    }

    // Botões Journal e Conference
    public void OnSelectCategory(string category)
    {
        panelArticleCategory.SetActive(false);
        panelList.SetActive(true);
        wheelController.LoadPapers(category.ToLower());
    }

    // Botão voltar
    public void OnBack()
    {
        if (panelArticleCategory.activeSelf)
        {
            panelArticleCategory.SetActive(false);
            panelSelection.SetActive(true);
        }
        else if (panelList.activeSelf)
        {
            panelList.SetActive(false);
            if (selectedType == "paper")
                panelArticleCategory.SetActive(true);
            else
                panelSelection.SetActive(true);
        }
    }
}
