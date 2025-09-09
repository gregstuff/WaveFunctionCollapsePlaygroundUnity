using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class TilemapResolver : MonoBehaviour
{
    public abstract void ResolveTilemap(ConstraintModelSO constraintModel,
        Action<Vector2Int, TileBase> TileBaseChangedCallback);
}

