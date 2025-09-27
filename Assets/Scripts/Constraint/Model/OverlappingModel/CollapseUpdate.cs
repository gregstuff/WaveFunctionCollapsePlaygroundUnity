using UnityEngine.Tilemaps;
using UnityEngine;

public sealed class CollapseUpdate
{
    public Vector2Int Cell;      // WFC cell that just collapsed (grid coords)
    public int N;                // pattern size
    public int PatternId;        // optional: for logs
    public TileBase[] Tiles;     // length N*N, row-major

    // Region in output tilemap covered by this pattern
    public RectInt OutputRect => new RectInt(Cell.x, Cell.y, N, N);

    // Map an output position inside OutputRect to the corresponding tile
    public TileBase TileAt(Vector2Int outPos)
    {
        int lx = outPos.x - Cell.x;     // local x in [0..N-1]
        int ly = outPos.y - Cell.y;     // local y in [0..N-1]
        return Tiles[lx + ly * N];
    }
}
