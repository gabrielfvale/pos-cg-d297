using UnityEngine;
using UnityEngine.UI;

namespace PaperCave
{
    /// <summary>
    /// Normaliza os eixos X e Y de uma imagem (Image ou RawImage) — calcula a
    /// proporção (aspect ratio) a partir das dimensões reais em pixels — e aplica
    /// uma correção ANTI-DISTORÇÃO: força o RectTransform a manter sempre uma
    /// escala UNIFORME (scaleX == scaleY), mesmo que o parent tenha escala
    /// não-uniforme ou que o slot disponível tenha proporção diferente da imagem.
    ///
    /// Normalizar = medir a forma da imagem (aspect ratio / par [0,1]).
    /// Anti-distorção = usar essa medida para garantir que a imagem nunca seja
    /// esticada de forma diferente em X e Y — ela sempre cabe inteira dentro da
    /// área alvo (estratégia "contain"), preservando sua proporção original.
    ///
    /// Basta colocar este componente no mesmo GameObject que tem o Image ou
    /// RawImage. Se areaWorldWidth/areaWorldHeight não forem definidos manualmente,
    /// o script usa o tamanho atual do RectTransform como referência.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class ImageNormalizer : MonoBehaviour
    {
        public enum Orientation { Unknown, Square, Landscape, Portrait }

        [Header("Normalização (somente leitura)")]
        [Tooltip("Largura/Altura em pixels reais, sem normalização.")]
        public Vector2 rawSize;

        [Tooltip("Razão width/height. >1 = landscape, <1 = portrait, =1 = quadrada.")]
        public float aspectRatio = 1f;

        [Tooltip("Eixos normalizados para o intervalo [0,1] — o maior eixo sempre vale 1.")]
        public Vector2 normalizedSize = Vector2.one;

        public Orientation orientation = Orientation.Unknown;

        [Header("Anti-distorção")]
        [Tooltip("Se true, corrige automaticamente a escala (uniforme) sempre que o sprite/textura mudar.")]
        public bool autoFix = true;

        [Tooltip("Largura da área alvo (world units). Se zero, usa o tamanho atual do RectTransform na primeira execução.")]
        public float areaWorldWidth = 0f;

        [Tooltip("Altura da área alvo (world units). Se zero, usa o tamanho atual do RectTransform na primeira execução.")]
        public float areaWorldHeight = 0f;

        private Image          _image;
        private RawImage       _rawImage;
        private RectTransform  _rt;
        private Sprite         _lastSprite;
        private Texture        _lastTexture;
        private bool           _areaInitialized;

        void Awake()
        {
            _image    = GetComponent<Image>();
            _rawImage = GetComponent<RawImage>();
            _rt       = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            RecalculateAndFix();
        }

        void Update()
        {
            bool changed = (_image != null && _image.sprite != _lastSprite)
                        || (_rawImage != null && _rawImage.texture != _lastTexture);

            if (changed) RecalculateAndFix();
        }

        /// <summary>Define a área alvo (world units) manualmente e reaplica a correção.</summary>
        public void SetArea(float worldWidth, float worldHeight)
        {
            areaWorldWidth   = worldWidth;
            areaWorldHeight  = worldHeight;
            _areaInitialized = true;
            RecalculateAndFix();
        }

        /// <summary>Recalcula a normalização e, se autoFix estiver ativo, corrige a escala.</summary>
        [ContextMenu("Recalculate And Fix")]
        public void RecalculateAndFix()
        {
            Recalculate();
            if (autoFix) FixDistortion();
        }

        /// <summary>Apenas mede a proporção da imagem atual, sem alterar nada na cena.</summary>
        public void Recalculate()
        {
            float w = 0f, h = 0f;

            if (_image != null && _image.sprite != null)
            {
                _lastSprite = _image.sprite;
                w = _image.sprite.rect.width;
                h = _image.sprite.rect.height;
            }
            else if (_rawImage != null && _rawImage.texture != null)
            {
                _lastTexture = _rawImage.texture;
                w = _rawImage.texture.width;
                h = _rawImage.texture.height;
            }

            if (w <= 0f || h <= 0f)
            {
                rawSize        = Vector2.zero;
                aspectRatio     = 1f;
                normalizedSize  = Vector2.one;
                orientation     = Orientation.Unknown;
                return;
            }

            rawSize     = new Vector2(w, h);
            aspectRatio = w / h;

            float maxDim = Mathf.Max(w, h);
            normalizedSize = new Vector2(w / maxDim, h / maxDim);

            const float tolerance = 0.02f;
            if (Mathf.Abs(aspectRatio - 1f) < tolerance)
                orientation = Orientation.Square;
            else
                orientation = aspectRatio > 1f ? Orientation.Landscape : Orientation.Portrait;
        }

        /// <summary>
        /// Aplica a correção anti-distorção: calcula a maior escala UNIFORME que
        /// cabe a imagem inteira dentro da área alvo, sem esticar X e Y de forma
        /// diferente (estratégia "contain").
        /// </summary>
        public void FixDistortion()
        {
            if (_rt == null || rawSize.x <= 0f || rawSize.y <= 0f) return;

            // Se a área alvo não foi definida manualmente, usa o tamanho atual do
            // RectTransform (em world units) como referência na primeira execução.
            if (!_areaInitialized)
            {
                var corners = new Vector3[4];
                _rt.GetWorldCorners(corners);
                areaWorldWidth   = Vector3.Distance(corners[0], corners[3]);
                areaWorldHeight  = Vector3.Distance(corners[0], corners[1]);
                _areaInitialized = true;
            }

            if (areaWorldWidth <= 0f || areaWorldHeight <= 0f) return;

            // sizeDelta original do RectTransform define a relação entre canvas
            // units e a escala — mantemos sizeDelta fixo e variamos só a ESCALA,
            // sempre igual em X e Y, para nunca distorcer a imagem.
            float scaleForW = areaWorldWidth  / _rt.sizeDelta.x;
            float scaleForH = areaWorldHeight / _rt.sizeDelta.y;
            float uniform   = Mathf.Min(scaleForW, scaleForH); // "contain": cabe inteira, sem cortar

            float zScale = transform.localScale.z != 0f ? transform.localScale.z : 1f;
            transform.localScale = new Vector3(uniform, uniform, zScale);

            // Image.preserveAspect não compensa escala não-uniforme do parent —
            // como já garantimos escala uniforme aqui, pode ficar desligado.
            if (_image != null) _image.preserveAspect = false;
        }
    }
}