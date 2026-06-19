using UnityEngine;

namespace PaperCave
{
    /// <summary>
    /// Mantém um objeto raiz da cena (tabela/imagem) posicionado sobre um card.
    /// O objeto é um Canvas World Space independente — fora da hierarquia do card
    /// para que os BoxColliders funcionem corretamente no Physics Engine.
    /// </summary>
    public class CardFollower : MonoBehaviour
    {
        [Tooltip("O card que este objeto deve seguir.")]
        public Transform target;

        [Tooltip("Offset em local space do target (calculado automaticamente).")]
        public Vector3 localOffset;

        void LateUpdate()
        {
            if (target == null || !gameObject.activeSelf) return;

            // Mantém posição e rotação coladas ao card
            transform.position = target.TransformPoint(localOffset);
            transform.rotation = target.rotation;
        }
    }
}
