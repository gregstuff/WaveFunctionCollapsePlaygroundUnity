using UnityEngine.Tilemaps;
using UnityEngine;

public readonly struct PaintOp
{
    public readonly Vector3Int[] Positions;
    public readonly TileBase[] Tiles;
    public PaintOp(Vector3Int[] positions, TileBase[] tiles)
    {
        Positions = positions;
        Tiles = tiles;
    }
}