using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class ITilemapResolver : MonoBehaviour
{
    public abstract void ResolveTilemap(ConstraintModelSO constraintModel,
        Action<Vector2Int, TileBase> TileBaseChangedCallback);
}

