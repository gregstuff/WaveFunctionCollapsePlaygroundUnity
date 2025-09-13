using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public sealed class Pattern : ISerializationCallbackReceiver
{
    [SerializeField] private int width;   // X dimension (columns)
    [SerializeField] private int height;  // Y dimension (rows)
    [SerializeField] private TileBase[] tiles; // row-major: idx = y * width + x

    [NonSerialized] private IReadOnlyList<TileBase> tilesReadonly;
    [NonSerialized] private bool hashed;
    [NonSerialized] private int cachedHash;

    public int Width => width;
    public int Height => height;

    // Read-only list view over the flat array
    public IReadOnlyList<TileBase> Tiles => tilesReadonly ?? tiles;

    // 2D view with (y, x) order
    public TileBase this[int y, int x] => tiles[y * width + x];

    public bool InBounds(int y, int x) =>
        (uint)y < (uint)height && (uint)x < (uint)width;

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
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!ReferenceEquals(a[i], b[i])) return false;

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
        hc.Add(width);
        hc.Add(height);
        if (tiles != null)
            for (int i = 0; i < tiles.Length; i++) hc.Add(tiles[i]);
        return hc.ToHashCode();
    }

    // Serialization hooks
    public void OnAfterDeserialize() => Seal();
    public void OnBeforeSerialize() { }

    private void Seal()
    {
        if (tiles == null) tiles = Array.Empty<TileBase>();
        tilesReadonly = Array.AsReadOnly(tiles);
        cachedHash = ComputeHash();
        hashed = true;
    }
}
