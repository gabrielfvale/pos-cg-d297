using UnityEngine;

namespace PaperCave
{
    [RequireComponent(typeof(Collider))]
    public class StackFlipButton : MonoBehaviour
    {
        public StackFlipController target;
        public int direction = 1; // +1 = next, -1 = prev

        void OnMouseDown()
        {
            if (direction > 0) target?.Next();
            else               target?.Prev();
        }
    }
}
