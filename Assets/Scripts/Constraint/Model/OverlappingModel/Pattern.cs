using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class Pattern
{
    [SerializeField] public int Width;
    [SerializeField] public int Height;
    [SerializeField] public TileBase[] Tiles;

    public Pattern(int width, int height, TileBase[] tiles)
    {
        Width = width; Height = height; Tiles = tiles;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is null || obj.GetType() != typeof(Pattern)) return false;

        var other = (Pattern)obj;

        if (Width != other.Width || Height != other.Height) return false;
        if (Tiles.Length != other.Tiles.Length) return false;

        for (int i = 0; i < Tiles.Length; i++)
            if (Tiles[i] != other.Tiles[i]) return false;

        return true;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Width);
        hc.Add(Height);
        foreach (var c in Tiles) hc.Add(c);
        return hc.ToHashCode();
    }

    public static bool operator ==(Pattern? left, Pattern? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Pattern? left, Pattern? right)
        => !(left == right);
}
