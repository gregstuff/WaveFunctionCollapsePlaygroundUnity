using UnityEngine;

public abstract class ConstraintBuilder : MonoBehaviour
{

    [ContextMenu("Generate Constraints")]
    public abstract void GenerateConstraints();

}