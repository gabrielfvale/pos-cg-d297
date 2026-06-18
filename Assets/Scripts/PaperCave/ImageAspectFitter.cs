using UnityEngine;
using UnityEngine.UI;

namespace PaperCave
{
    /// <summary>
    /// Ajusta a escala de uma Image para caber dentro de uma área alvo
    /// (em world units) preservando a proporção (aspect ratio) do sprite atual,
    /// evitando distorção mesmo quando outras escalas no transform são não-uniformes.
    ///
    /// Use FitToArea(worldWidth, worldHeight) uma vez para configurar a área,
    /// e chame Refit() sempre que o sprite mudar (ex: após um hover trocar a imagem).
    /// O LateUpdate já detecta a troca de sprite automaticamente.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    public class ImageAspectFitter : MonoBehaviour
    {
        [Tooltip("Largura da área alvo em world units.")]
        public float areaWorldWidth = 1f;

        [Tooltip("Altura da área alvo em world units.")]
        public float areaWorldHeight = 1f;

        private Image         _image;
        private RectTransform _rt;
        private Sprite        _lastSprite;

        void Awake()
        {
            _image = GetComponent<Image>();
            _rt    = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            Refit();
        }

        void LateUpdate()
        {
            // Detecta troca de sprite (ex: hover na tabela) e reajusta automaticamente
            if (_image != null && _image.sprite != _lastSprite)
                Refit();
        }

        /// <summary>Define a área alvo (world units) e reajusta imediatamente.</summary>
        public void FitToArea(float worldWidth, float worldHeight)
        {
            areaWorldWidth  = worldWidth;
            areaWorldHeight = worldHeight;
            Refit();
        }

        /// <summary>Recalcula a escala uniforme para a proporção do sprite atual.</summary>
        public void Refit()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_rt == null)    _rt    = GetComponent<RectTransform>();
            if (_image.sprite == null) return;

            _lastSprite = _image.sprite;

            float spriteW = _image.sprite.rect.width;
            float spriteH = _image.sprite.rect.height;
            if (spriteW <= 0f || spriteH <= 0f) return;

            float spriteAspect = spriteW / spriteH;
            float areaAspect   = areaWorldWidth / areaWorldHeight;

            // "Contain": a imagem cabe inteira dentro da área, sem distorcer.
            float targetWorldW, targetWorldH;
            if (spriteAspect > areaAspect)
            {
                // sprite relativamente mais largo que a área -> largura é o limitante
                targetWorldW = areaWorldWidth;
                targetWorldH = areaWorldWidth / spriteAspect;
            }
            else
            {
                // sprite relativamente mais alto que a área -> altura é o limitante
                targetWorldH = areaWorldHeight;
                targetWorldW = areaWorldHeight * spriteAspect;
            }

            // sizeDelta original do prefab (geralmente quadrado, ex: 30x30) define
            // a relação entre canvas units e mundo. Mantemos sizeDelta fixo e variamos
            // apenas a ESCALA — sempre uniforme (x == y) — para nunca distorcer a imagem.
            float baseSizeX = _rt.sizeDelta.x;
            float baseSizeY = _rt.sizeDelta.y;

            float scaleForW = targetWorldW / baseSizeX;
            float scaleForH = targetWorldH / baseSizeY;
            float uniform   = Mathf.Min(scaleForW, scaleForH);

            float zScale = transform.localScale.z != 0f ? transform.localScale.z : 1f;
            transform.localScale = new Vector3(uniform, uniform, zScale);

            _image.preserveAspect = false; // não é mais necessário — a escala já é exata
        }
    }
}