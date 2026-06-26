using TMPro;
using UnityEngine;

namespace PaperCave
{
    public class StackFlipController : MonoBehaviour
    {
        public GameObject[]        panels;
        public TextMeshProUGUI     counterText;
        public AnimationFrameView3D.Transition transition = AnimationFrameView3D.Transition.Fade;

        int _current = 0;

        void Start() => Apply();

        public void Next() { _current = (_current + 1) % panels.Length; Apply(); }
        public void Prev() { _current = (_current - 1 + panels.Length) % panels.Length; Apply(); }

        void Apply()
        {
            for (int i = 0; i < panels.Length; i++)
                panels[i].SetActive(i == _current);
            if (counterText) counterText.text = $"{_current + 1} / {panels.Length}";
        }
    }
}
