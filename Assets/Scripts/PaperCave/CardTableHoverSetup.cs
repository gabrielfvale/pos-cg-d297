using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace PaperCave
{
    [RequireComponent(typeof(Card3D))]
    public class CardTableHoverSetup : MonoBehaviour
    {
        [Tooltip("A tabela instanciada (TabelaCard04)")]
        public GameObject tableObject;

        [Tooltip("O Image que receberá o sprite ao hover (ImagemCard05)")]
        public Image targetImage;

        [Tooltip("BorderPulse do card cujas bordas devem pulsar ao hover. Resolvido automaticamente a partir do targetImage se não atribuído.")]
        public BorderPulse targetCardBorder;

        public void RunSetup()
        {
            if (tableObject == null) return;

            if (targetCardBorder == null && targetImage != null)
                targetCardBorder = targetImage.GetComponentInParent<BorderPulse>();

            var hovers      = tableObject.GetComponentsInChildren<TableRowHover>(true);
            var targetField = typeof(TableRowHover).GetField("_targetImage",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var worldCorners = new Vector3[4];

            foreach (var h in hovers)
            {
                // Injeta Image alvo e sprites
                h.useImageSwap      = targetImage != null;
                h.imageTargetName   = "";
                h.targetCardBorder  = targetCardBorder;
                targetField?.SetValue(h, targetImage);

                var sprites = new Sprite[7];
                if (h.rowSprites != null)
                    for (int i = 0; i < Mathf.Min(h.rowSprites.Length, 7); i++)
                        sprites[i] = h.rowSprites[i];
                var loaded = Resources.Load<Sprite>("i" + (h.rowIndex + 1));
                if (loaded != null) sprites[h.rowIndex] = loaded;
                h.rowSprites = sprites;

                // Redimensiona BoxCollider para o tamanho world correto
                var col = h.GetComponent<BoxCollider>();
                if (col == null) continue;

                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;

                foreach (Transform child in h.transform)
                {
                    var crt = child.GetComponent<RectTransform>();
                    if (crt == null) continue;
                    crt.GetWorldCorners(worldCorners);
                    foreach (var c in worldCorners)
                    {
                        minX = Mathf.Min(minX, c.x);
                        maxX = Mathf.Max(maxX, c.x);
                        minY = Mathf.Min(minY, c.y);
                        maxY = Mathf.Max(maxY, c.y);
                    }
                }

                if (minX == float.MaxValue) continue;

                float worldW  = maxX - minX;
                float worldH  = maxY - minY;
                var   ls      = h.transform.lossyScale;
                float localW  = ls.x > 0 ? worldW / ls.x : worldW;
                float localH  = ls.y > 0 ? worldH / ls.y : worldH;

                var worldCenterXY = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, h.transform.position.z);
                var localCenter   = h.transform.InverseTransformPoint(worldCenterXY);

                col.size      = new Vector3(localW, localH, 0.1f);
                col.center    = new Vector3(localCenter.x, localCenter.y, 0f);
                col.isTrigger = true;
            }

            Debug.Log("[CardTableHoverSetup] " + hovers.Length + " rows configurados.");
        }
    }
}