using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public sealed class Pattern : ISerializationCallbackReceiver
{
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private TileBase[] tiles;

    [NonSerialized] private IReadOnlyList<TileBase> tilesReadonly;
    [NonSerialized] private bool hashed;
    [NonSerialized] private int cachedHash;

    public int Width => width;
    public int Height => height;
    public IReadOnlyList<TileBase> Tiles => tilesReadonly ?? tiles;

    public Pattern(int width, int height, TileBase[] tiles)
    {
        if (tiles == null) throw new ArgumentNullException(nameof(tiles));
        if (tiles.Length != width * height) throw new ArgumentException("tiles length must equal width*height");

        this.width = width;
        this.height = height;
        this.tiles = (TileBase[])tiles.Clone();

        Seal();
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not Pattern other) return false;
        if (width != other.width || height != other.height) return false;
        var a = tiles; var b = other.tiles;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (!ReferenceEquals(a[i], b[i])) return false;
        return true;
    }

    public override int GetHashCode()
    {
        if (hashed) return cachedHash;
        cachedHash = ComputeHash();
        hashed = true;
        return cachedHash;
    }

    private int ComputeHash()
    {
        var hc = new HashCode();
        hc.Add(width); hc.Add(height);
        foreach (var t in tiles) hc.Add(t);
        return hc.ToHashCode();
    }

    public void OnAfterDeserialize()
    {
        Seal();
    }

    private void Seal()
    {
        tilesReadonly = Array.AsReadOnly(tiles);

        cachedHash = ComputeHash();
        hashed = true;
    }

    public void OnBeforeSerialize()
    {

    }
}
