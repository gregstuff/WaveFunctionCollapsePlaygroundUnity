using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ProcGen/WFC/Overlap Model")]
public class OverlappingModelTileModelSO : ScriptableObject
{
    [SerializeField] public int N;

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

    [SerializeField] public List<PatternFrequency> patterns = new();
    [SerializeField] public List<PatternAdjacency> compatibilities = new();
}
