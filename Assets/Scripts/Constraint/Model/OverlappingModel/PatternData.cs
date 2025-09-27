using System;
using UnityEngine.Tilemaps;

[Serializable]
public class PatternData
{
    public int patternId;
    public TileBase[] tilePattern;
    public double weight;
}