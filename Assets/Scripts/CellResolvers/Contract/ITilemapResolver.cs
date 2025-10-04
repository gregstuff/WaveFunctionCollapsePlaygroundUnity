using System;
using UnityEngine;

public abstract class TilemapResolver : MonoBehaviour
{
    public abstract void ResolveTilemap(ConstraintModelSO constraintModel,
        Action<CollapseUpdate> TileBaseChangedCallback);
}
