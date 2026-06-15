using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperCave
{
    /// <summary>
    /// Makes a UI card freely draggable. On begin drag it is brought to the
    /// front (SetAsLastSibling); during drag it follows the pointer with no
    /// grid snapping.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        RectTransform _rt;
        RectTransform _parent;
        Vector2 _offset;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _parent = _rt.parent as RectTransform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Bring this card to the front so it draws above the others.
            transform.SetAsLastSibling();
            if (_parent == null) _parent = _rt.parent as RectTransform;

            Vector2 pointer;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parent, eventData.position, eventData.pressEventCamera, out pointer))
            {
                _offset = _rt.anchoredPosition - pointer;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_parent == null) return;

            Vector2 pointer;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parent, eventData.position, eventData.pressEventCamera, out pointer))
            {
                // Free positioning: just follow the pointer, no snapping.
                _rt.anchoredPosition = pointer + _offset;
            }
        }
    }
}
