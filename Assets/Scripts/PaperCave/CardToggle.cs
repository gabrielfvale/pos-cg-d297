using UnityEngine;
using UnityEngine.UI;

namespace PaperCave
{
    /// <summary>
    /// Toggles a card between its collapsed and expanded views when the card's
    /// root Button is clicked. The root RectTransform is resized between the
    /// collapsed height and the full expanded height. The pivot is expected to
    /// be at the top (y = 1) so the card grows downward.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardToggle : MonoBehaviour
    {
        public GameObject collapsedView;
        public GameObject expandedView;
        public float collapsedHeight = 80f;
        public float expandedHeight = 420f;
        public bool expanded = false;

        RectTransform _rt;

        void Awake()
        {
            _rt = (RectTransform)transform;

            var button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(Toggle);

            Apply();
        }

        public void Toggle()
        {
            expanded = !expanded;
            Apply();
        }

        void Apply()
        {
            if (collapsedView != null) collapsedView.SetActive(!expanded);
            if (expandedView != null) expandedView.SetActive(expanded);

            Vector2 size = _rt.sizeDelta;
            size.y = expanded ? expandedHeight : collapsedHeight;
            _rt.sizeDelta = size;
        }
    }
}
