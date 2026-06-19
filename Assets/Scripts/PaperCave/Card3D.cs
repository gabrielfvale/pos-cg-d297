using UnityEngine;

namespace PaperCave
{
    /// <summary>
    /// Per-card state for the world-space "PaperCave_Cards_3D" scene. Holds the
    /// resting pose, the collapsed/expanded view roots (a World Space Canvas
    /// child) plus an optional 3D "extra" root (used for the animation card's
    /// physical button planes), and animates the expand interaction: on click
    /// the card scales up 1.4x and floats forward 0.5 units toward the camera.
    ///
    /// All pointer input is driven externally by <see cref="Card3DController"/>;
    /// this component only owns the per-card pose and its tween.
    /// </summary>
    public class Card3D : MonoBehaviour
    {
        [Header("Views (toggled on expand)")]
        public GameObject collapsedView;   // canvas child
        public GameObject expandedView;    // canvas child
        public GameObject expandedExtra;   // optional 3D child (e.g. anim buttons)

        [Header("Expand behaviour")]
        public float expandScale = 1.4f;
        public float floatForward = 0.5f;  // world units toward the camera (-Z)
        public float tweenDuration = 0.18f;

        public bool Expanded { get; private set; }

        // Resting pose (updated when the card is dropped after a drag).
        Vector3 _restPosition;
        Vector3 _baseScale;

        // Tween state.
        bool _tweening;
        float _t;
        Vector3 _fromPos, _toPos, _fromScale, _toScale;

        void Awake()
        {
            _restPosition = transform.position;
            _baseScale = transform.localScale;
            ApplyViews();
        }

        void Update()
        {
            if (!_tweening) return;

            _t += Time.deltaTime / Mathf.Max(0.0001f, tweenDuration);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t));
            transform.position = Vector3.Lerp(_fromPos, _toPos, k);
            transform.localScale = Vector3.Lerp(_fromScale, _toScale, k);
            if (_t >= 1f) _tweening = false;
        }

        void ApplyViews()
        {
            if (collapsedView) collapsedView.SetActive(!Expanded);
            if (expandedView) expandedView.SetActive(Expanded);
            if (expandedExtra) expandedExtra.SetActive(Expanded);
        }

        /// <summary>Click handler: flip between collapsed and expanded.</summary>
        public void Toggle()
        {
            Expanded = !Expanded;
            ApplyViews();

            Vector3 pos = _restPosition;
            Vector3 scale = _baseScale;
            if (Expanded)
            {
                // -Z is toward the camera in this scene.
                pos = _restPosition + new Vector3(0f, 0f, -floatForward);
                scale = _baseScale * expandScale;
            }
            StartTween(pos, scale);
        }

        /// <summary>Called by the controller when a drag begins; halts any tween.</summary>
        public void BeginDrag()
        {
            _tweening = false;
        }

        /// <summary>Called by the controller when the card is dropped after a drag.</summary>
        public void SetRest(Vector3 newRest)
        {
            _restPosition = newRest;
            // Keep the expanded float offset consistent with the new rest pose.
            if (Expanded)
            {
                transform.position = _restPosition + new Vector3(0f, 0f, -floatForward);
            }
            else
            {
                transform.position = _restPosition;
            }
        }

        void StartTween(Vector3 toPos, Vector3 toScale)
        {
            _fromPos = transform.position;
            _fromScale = transform.localScale;
            _toPos = toPos;
            _toScale = toScale;
            _t = 0f;
            _tweening = true;
        }
    }
}
