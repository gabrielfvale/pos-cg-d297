using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WheelController : MonoBehaviour
{
    [Header("Wheel")]
    public RectTransform wheelContainer;
    public GameObject wheelItemPrefab;
    public float wheelRadius = 150f;

    [Header("Details")]
    public TextMeshProUGUI txtEvent;
    public TextMeshProUGUI txtTitle;

    [Header("Authors")]
    public GameObject authorCardPrefab;
    public Transform authorCardContainer;
    public Button btnPrev;
    public Button btnNext;

    [Header("Abstract")]
    public GameObject panelAbstract;
    public TextMeshProUGUI txtAbstract;
    public Button btnAbstract;

    [Header("Carousel")]
    public float carouselInterval = 3f; // segundos entre cada avanço
    private float carouselTimer = 0f;
    private bool autoCarousel = false;

    private PaperData[] papers;
    private int activeIndex = 0;
    private int authorIndex = 0;

    void Start()
    {
        btnPrev.onClick.AddListener(PrevAuthor);
        btnNext.onClick.AddListener(NextAuthor);
        btnAbstract.onClick.AddListener(OpenAbstract);

        if (panelAbstract != null)
            panelAbstract.SetActive(false);
    }

    void Update()
    {
        if (!autoCarousel) return;
        if (papers == null || papers.Length == 0) return;

        carouselTimer -= Time.deltaTime;

        if (carouselTimer <= 0f)
        {
            carouselTimer = carouselInterval;
            authorIndex = (authorIndex + 1) % papers[activeIndex].authors.Length;
            ShowAuthor();
        }
    }

    void OnEnable()
    {
        if (papers != null && papers.Length > 0)
            ShowPaper(activeIndex);
    }

    public void LoadPapers(string type)
    {
        activeIndex = 0;
        authorIndex = 0;

        var all = PaperDatabase.GetAll();
        papers = System.Array.FindAll(all, p => p.type == type);

        if (papers.Length == 0)
        {
            Debug.LogWarning("Nenhum paper encontrado para o tipo: " + type);
            return;
        }

        BuildWheel();
        ShowPaper(0);
    }

    void BuildWheel()
    {
        foreach (Transform child in wheelContainer)
            Destroy(child.gameObject);

        float angleStep = 360f / papers.Length;

        for (int i = 0; i < papers.Length; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Sin(angle) * wheelRadius;
            float y = Mathf.Cos(angle) * wheelRadius;

            GameObject item = Instantiate(wheelItemPrefab, wheelContainer);
            RectTransform rt = item.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);

            TextMeshProUGUI label = item.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = $"Option {i + 1:00}";

            int index = i;
            Button btn = item.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => ShowPaper(index));
        }
    }

    void ShowPaper(int index)
    {
        if (papers == null || papers.Length == 0) return;

        activeIndex = Mathf.Clamp(index, 0, papers.Length - 1);
        authorIndex = 0;

        if (txtEvent != null) txtEvent.text = papers[activeIndex].eventName;
        if (txtTitle != null) txtTitle.text = papers[activeIndex].title;

        ShowAuthor();

        if (panelAbstract != null)
            panelAbstract.SetActive(false);

        // Ativa carrossel só se tiver mais de 2 autores
        autoCarousel = papers[activeIndex].authors.Length > 2;
        carouselTimer = carouselInterval;
    }

    void ShowAuthor()
    {
        if (papers == null || papers.Length == 0) return;
        var authors = papers[activeIndex].authors;
        if (authors == null || authors.Length == 0) return;

        // Limpa cards anteriores
        foreach (Transform child in authorCardContainer)
            Destroy(child.gameObject);

        // Mostra até 2 autores a partir do authorIndex
        int visibleCount = Mathf.Min(2, authors.Length);

        for (int i = 0; i < visibleCount; i++)
        {
            int idx = (authorIndex + i) % authors.Length;
            var author = authors[idx];

            GameObject card = Instantiate(authorCardPrefab, authorCardContainer);
            var texts = card.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = author.name;
            if (texts.Length > 1) texts[1].text = author.university;
        }
    }
    void PrevAuthor()
    {
        if (papers == null || papers.Length == 0) return;
        if (papers[activeIndex].authors == null || papers[activeIndex].authors.Length == 0) return;

        authorIndex--;
        if (authorIndex < 0)
            authorIndex = papers[activeIndex].authors.Length - 1;

        carouselTimer = carouselInterval; // reseta o timer
        ShowAuthor();
    }

    void NextAuthor()
    {
        if (papers == null || papers.Length == 0) return;
        if (papers[activeIndex].authors == null || papers[activeIndex].authors.Length == 0) return;

        authorIndex++;
        if (authorIndex >= papers[activeIndex].authors.Length)
            authorIndex = 0;

        carouselTimer = carouselInterval; // reseta o timer
        ShowAuthor();
    }

    void OpenAbstract()
    {
        if (papers == null || papers.Length == 0) return;
        if (panelAbstract != null) panelAbstract.SetActive(true);
        if (txtAbstract != null) txtAbstract.text = papers[activeIndex].abstractText;
    }
}
