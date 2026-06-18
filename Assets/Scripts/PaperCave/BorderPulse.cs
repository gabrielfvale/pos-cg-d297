using UnityEngine;

namespace PaperCave
{
    /// <summary>
    /// Faz as 4 bordas (Border_Top/Bottom/Left/Right) de um card pulsarem
    /// rapidamente entre a emissão original e uma cor de destaque, voltando
    /// para a cor original ao final de cada ciclo.
    /// Chame StartPulse() para iniciar e StopPulse() para parar.
    /// </summary>
    public class BorderPulse : MonoBehaviour
    {
        [Tooltip("Velocidade do pulso (ciclos por segundo).")]
        public float pulseSpeed = 6f;

        [Tooltip("Cor de destaque no pico do pulso (RGB — a intensidade final usa pulseIntensity).")]
        public Color pulseColor = new Color(0.2f, 0.6f, 1f, 1f); // azul forte/destaque

        [Tooltip("Intensidade de emissão da cor de destaque no pico.")]
        public float pulseIntensity = 4f;

        private static readonly string[] BorderNames =
            { "Border_Top", "Border_Bottom", "Border_Left", "Border_Right" };

        private MeshRenderer[] _borders;
        private Material[]     _instancedMats;
        private Color[]        _baseEmission;
        private bool           _pulsing;
        private float          _t;

        void Awake()
        {
            CacheBorders();
        }

        private void CacheBorders()
        {
            _borders       = new MeshRenderer[BorderNames.Length];
            _instancedMats = new Material[BorderNames.Length];
            _baseEmission  = new Color[BorderNames.Length];

            for (int i = 0; i < BorderNames.Length; i++)
            {
                var t = transform.Find(BorderNames[i]);
                if (t == null) continue;

                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                // Instancia o material para não afetar outros cards que compartilhem o mesmo asset
                var instMat = mr.material; // .material (não sharedMaterial) já cria instância
                instMat.EnableKeyword("_EMISSION");

                _borders[i]       = mr;
                _instancedMats[i] = instMat;
                _baseEmission[i]  = instMat.GetColor("_EmissionColor");
            }
        }

        public void StartPulse()
        {
            if (_borders == null) CacheBorders();
            _pulsing = true;
            _t = 0f;
        }

        public void StopPulse()
        {
            _pulsing = false;
            // Restaura emissão original imediatamente
            for (int i = 0; i < _instancedMats.Length; i++)
            {
                if (_instancedMats[i] == null) continue;
                _instancedMats[i].SetColor("_EmissionColor", _baseEmission[i]);
            }
        }

        void Update()
        {
            if (!_pulsing) return;

            _t += Time.deltaTime * pulseSpeed;
            // Onda 0..1..0 suave (sin remapeado) — 0 = cor original, 1 = cor de destaque
            float wave = (Mathf.Sin(_t * Mathf.PI * 2f) + 1f) * 0.5f;

            Color peak = pulseColor * pulseIntensity;

            for (int i = 0; i < _instancedMats.Length; i++)
            {
                if (_instancedMats[i] == null) continue;
                Color blended = Color.Lerp(_baseEmission[i], peak, wave);
                _instancedMats[i].SetColor("_EmissionColor", blended);
            }
        }
    }
}