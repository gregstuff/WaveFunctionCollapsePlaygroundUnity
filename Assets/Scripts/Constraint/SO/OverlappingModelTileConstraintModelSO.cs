using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "ProcGen/WFC/Overlap Model")]
public class OverlappingModelTileModelSO : ConstraintModelSO
{
    [SerializeField] public int N;
    [SerializeField] public List<PatternFrequency> patterns = new();
    [SerializeField] public List<PatternAdjacency> compatibilities = new();

    [Serializable]
    public class PatternFrequency
    {
        public Pattern pattern;
        public int count;
    }

    [Serializable]
    public class DirectionAdjacency
    {
        public Direction direction;
        public Pattern target;
        public int count;
    }

    [Serializable]
    public class PatternAdjacency
    {
        public Pattern source;
        public List<DirectionAdjacency> edges = new();
    }



    public override TileBase CollapseCell(Vector2Int pos)
    {
        throw new NotImplementedException();
    }

    public override void EnqueueNeighbours(Cell cell, Queue<Cell> candidates)
    {
        throw new NotImplementedException();
    }

    public override Cell GetNext()
    {
        throw new NotImplementedException();
    }

    public override void Init(Vector2Int dimensions)
    {
        throw new NotImplementedException();
    }

    public override EntropyResult ReduceByNeighbors(Cell cell)
    {
        throw new NotImplementedException();
    }



}
