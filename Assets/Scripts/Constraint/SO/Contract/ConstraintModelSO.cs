using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class ConstraintModelSO : ScriptableObject
{

    [Header("WFC Controls")]
    [SerializeField] public bool IgnoreContradictions;

    [Header("Tiles")]
    [SerializeField] public TileBase DefaultTile;

    public abstract void Init(Vector2Int dimensions);
    public abstract TileBase CollapseCell(Vector2Int pos);
    public abstract Cell GetNext();
    public abstract void EnqueueNeighbours(Cell cell, Queue<Cell> candidates);
    public abstract EntropyResult ReduceByNeighbors(Cell cell);
}
