using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class ITilemapResolver : MonoBehaviour
{
    public abstract void ResolveTilemap(ConstraintModelSO constraintModel,
        Dictionary<Vector2Int, Cell> cells,
        Action<Cell> TileBaseChangedCallback);
}
