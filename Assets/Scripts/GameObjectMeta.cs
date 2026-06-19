using UnityEngine;

/// <summary>
/// Componente que armazena os metadados originais do JSON
/// em cada GameObject instanciado na cena.
/// </summary>
public class GameObjectMeta : MonoBehaviour
{
    [Header("Metadados do JSON")]
    public string suggestedName;
    public string type;
    public string category;
    public string interactionType;

    [TextArea(2, 4)] public string conceptualOrigin;
    [TextArea(2, 4)] public string visualMetaphor;
    [TextArea(2, 4)] public string behaviourHint;
    [TextArea(2, 4)] public string whyThisFive;
}
