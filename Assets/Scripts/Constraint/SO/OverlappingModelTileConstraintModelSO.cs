using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "ProcGen/WFC/Overlap Model")]
public class OverlappingModelTileModelSO : ConstraintModelSO
{
    [Header("Model Data (input)")]
    [SerializeField] public int N;
    [SerializeField] public int patternSize;
    [SerializeField] public List<PatternData> patterns;
    [SerializeField] public List<PatternAdjacency> adjacencies;

    [Header("Performance / Quality")]
    [SerializeField] private int minPatternFrequency = 1;

    [Serializable]
    public class PatternData
    {
        public int patternId;
        public List<TileBase> tiles;
        public double weight;
    }

    [Serializable]
    public class PatternAdjacency
    {
        public int sourcePatternId;
        public Direction direction;
        public List<int> compatiblePatternIds = new List<int>();
    }

    public OverlappingModelTileModelSO()
    {
        patterns = new List<PatternData>();
        adjacencies = new List<PatternAdjacency>();
    }

    public override void Init(Vector2Int dimensions) { }
    public override TileBase CollapseCell(Vector2Int pos) { return null; }
    public override void EnqueueNeighbours(Cell cell, Queue<Cell> candidates) { }
    public override Cell GetNext() { return null; }
    public override EntropyResult ReduceByNeighbors(Cell cell) { return null; }
}