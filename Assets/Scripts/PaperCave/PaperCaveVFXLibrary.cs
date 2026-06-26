using System.Collections.Generic;
using UnityEngine;

namespace PaperCave
{
    [CreateAssetMenu(menuName = "PaperCave/VFX Library", fileName = "PaperCaveVFXLibrary")]
    public class PaperCaveVFXLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Preset
        {
            [Tooltip("Nome legível para identificar o preset no Inspector.")]
            public string presetName = "Lightning Aura";

            [Tooltip("Prefab VFX a instanciar quando a carta for expandida.")]
            public GameObject prefab;

            [Tooltip("Offset local relativo ao centro da carta.")]
            public Vector3 localOffset = Vector3.zero;

            [Tooltip("Multiplicador de escala aplicado ao prefab instanciado.")]
            public Vector3 scaleMultiplier = Vector3.one;

            [Tooltip("Segundos até destruir a instância (0 = não destruir).")]
            public float destroyAfter = 5f;

            [Tooltip("Se true, o VFX segue a carta quando arrastada.")]
            public bool followTarget = true;
        }

        [Tooltip("Lista de presets VFX. Índice 0 = default para todas as cartas.")]
        public List<Preset> presets = new();
    }
}
