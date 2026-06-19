using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PaperCave
{
    /// <summary>
    /// Single scene-level input driver for the 3D cards. Uses the new Input
    /// System (this project runs with Active Input Handling = Input System).
    ///
    /// On press it raycasts from the camera: a hit on a <see cref="Card3DButton"/>
    /// fires that button; a hit on a <see cref="Card3D"/> starts a press. A press
    /// that stays roughly in place is treated as a click (toggle expand); a press
    /// that moves past a threshold becomes a physics-style drag — the card follows
    /// the pointer across a plane at z = 0 and is pulled forward (z = -0.5) while
    /// held, then stays where it is dropped (no snap).
    /// </summary>
    public class Card3DController : MonoBehaviour
    {
        public Camera targetCamera;
        public float dragPlaneZ = 0f;
        public float heldForwardZ = -0.5f;
        public float clickPixelThreshold = 8f;

        Plane _plane;

        Card3D _active;
        Vector2 _pressScreenPos;
        Vector2 _dragOffset;   // card.xy - pointerWorld.xy at press
        bool _dragging;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            _plane = new Plane(Vector3.forward, new Vector3(0, 0, dragPlaneZ));
        }

        void Update()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            #if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 screen = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame) OnPress(screen);
            else if (mouse.leftButton.isPressed && _active != null) OnHold(screen);
            else if (mouse.leftButton.wasReleasedThisFrame && _active != null) OnRelease();
            #endif
        }

        void OnPress(Vector2 screen)
        {
            Ray ray = targetCamera.ScreenPointToRay(screen);
            RaycastHit[] hits = Physics.RaycastAll(ray, 200f);
            if (hits == null || hits.Length == 0) return;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var button = hit.collider.GetComponentInParent<Card3DButton>();
                if (button != null)
                {
                    button.Invoke();
                    _active = null;
                    return;
                }

                var card = hit.collider.GetComponentInParent<Card3D>();
                if (card != null)
                {
                    _active = card;
                    _pressScreenPos = screen;
                    _dragging = false;

                    Vector3 world = PlanePoint(screen);
                    _dragOffset = new Vector2(card.transform.position.x - world.x,
                                              card.transform.position.y - world.y);
                    return;
                }
            }
        }

        void OnHold(Vector2 screen)
        {
            if (!_dragging)
            {
                if (Vector2.Distance(screen, _pressScreenPos) < clickPixelThreshold) return;
                _dragging = true;
                _active.BeginDrag();
            }

            Vector3 world = PlanePoint(screen);
            _active.transform.position = new Vector3(world.x + _dragOffset.x,
                                                     world.y + _dragOffset.y,
                                                     heldForwardZ);
        }

        void OnRelease()
        {
            if (_dragging)
            {
                Vector3 p = _active.transform.position;
                p.z = dragPlaneZ;
                _active.transform.position = p;
                _active.SetRest(p);   // stays where dropped, no snap
            }
            else
            {
                _active.Toggle();     // it was a click
            }
            _active = null;
            _dragging = false;
        }

        Vector3 PlanePoint(Vector2 screen)
        {
            Ray ray = targetCamera.ScreenPointToRay(screen);
            if (_plane.Raycast(ray, out float enter)) return ray.GetPoint(enter);
            return Vector3.zero;
        }
    }
}
