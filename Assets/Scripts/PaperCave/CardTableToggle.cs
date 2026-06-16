using UnityEngine;

namespace PaperCave
{
    [RequireComponent(typeof(Card3D))]
    public class CardTableToggle : MonoBehaviour
    {
        [Tooltip("GameObject a mostrar quando o card estiver expandido.")]
        public GameObject tableObject;

        private Card3D              _card;
        private bool                _wasExpanded;
        private bool                _setupCalled;
        private CardTableHoverSetup _hoverSetup;

        void Awake()
        {
            _card       = GetComponent<Card3D>();
            _hoverSetup = GetComponent<CardTableHoverSetup>();
        }

        void Start()
        {
            _wasExpanded = _card.Expanded;
            Sync();
        }

        void Update()
        {
            if (_card.Expanded != _wasExpanded)
            {
                _wasExpanded = _card.Expanded;
                Sync();
            }
        }

        private void Sync()
        {
            if (tableObject == null) return;
            tableObject.SetActive(_card.Expanded);

            if (_card.Expanded && !_setupCalled && _hoverSetup != null)
            {
                _hoverSetup.RunSetup();
                _setupCalled = true;
            }
        }
    }
}