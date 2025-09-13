using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DebugPatternOutput : MonoBehaviour
{
    [SerializeField] private int area;
    [SerializeField] private int perRow = 10;
    [SerializeField] private Tilemap Tilemap;
    [SerializeField] private OverlappingModelTileModelSO OverlappingModel;

    [SerializeField] private TileBase validBorderTile;
    [SerializeField] private TileBase invalidBorderTile;

    [ContextMenu("Deploy patterns to tilemap")]
    public void DeployPattern()
    {
        ClearTileMap();

        int cx = 0, cy = 0, ptr = 0;
        foreach (var patternFreq in OverlappingModel.patterns)
        {
            ++ptr;

            for (int y = 0; y < area; ++y)
            {
                for (int x = 0; x < area; ++x)
                {
                    int resolvedX = cx + x, resolvedY = cy + y;

                    Vector3Int pos = new Vector3Int(resolvedX, resolvedY, 0);
                    Tilemap.SetTile(pos, patternFreq.pattern[y, x]);
                }
            }

            cx += area + 1;

            if (ptr >= perRow)
            {
                ptr = 0;
                cy += area + 1;
                cx = 0;
            }
        }
    }

    [ContextMenu("Clear tilemap")]
    public void ClearTileMap()
    {
        Tilemap.ClearAllTiles();
    }

    [ContextMenu("Sample Random Adjacency")]
    public void SampleRandomAdjacency()
    {
        void DrawPattern(int oY, int oX, Pattern p)
        {
            for (int y = 0; y < area; ++y)
            {
                for (int x = 0; x < area; ++x)
                {
                    Tilemap.SetTile(new Vector3Int(oX + x, oY + y), p[y, x]);
                }
            }
        }

        ClearTileMap();

        var selectedCompatibility =
            OverlappingModel.compatibilities[UnityEngine.Random.Range(0, OverlappingModel.compatibilities.Count)];

        DrawPattern(0, 0, selectedCompatibility.source);

        Dictionary<Direction, int> encounteredDirections = new Dictionary<Direction, int>();
        const int spacing = 1;
        int step = area + spacing;

        foreach (var directionAdjacency in selectedCompatibility.edges)
        {
            var dir = directionAdjacency.direction;
            if (!encounteredDirections.TryGetValue(dir, out var count))
                count = 0;

            count++;
            encounteredDirections[dir] = count;

            var v = dir.ToVector();
            var offset = v * (count * step);

            DrawPattern(offset.y, offset.x, directionAdjacency.target);
        }

    }

    [ContextMenu("Sample Random Overlap Adjacency")]
    public void SampleRandomOverlapAdjacency()
    {
        void DrawPattern(int oY, int oX, Pattern p)
        {
            for (int y = 0; y < area; ++y)
                for (int x = 0; x < area; ++x)
                    Tilemap.SetTile(new Vector3Int(oX + x, oY + y, 0), p[y, x]);
        }

        // Optional: draw a 1-tile border to indicate valid/invalid
        void DrawBorder(int oY, int oX, bool valid)
        {
            TileBase border = valid ? validBorderTile : invalidBorderTile;
            if (border == null) return;

            int x0 = oX, y0 = oY, x1 = oX + area - 1, y1 = oY + area - 1;
            for (int x = x0; x <= x1; x++)
            {
                Tilemap.SetTile(new Vector3Int(x, y0, 0), border);
                Tilemap.SetTile(new Vector3Int(x, y1, 0), border);
            }
            for (int y = y0; y <= y1; y++)
            {
                Tilemap.SetTile(new Vector3Int(x0, y, 0), border);
                Tilemap.SetTile(new Vector3Int(x1, y, 0), border);
            }
        }

        ClearTileMap();

        var selected =
            OverlappingModel.compatibilities[UnityEngine.Random.Range(0, OverlappingModel.compatibilities.Count)];

        // Draw source at origin and mark it as the reference
        DrawPattern(0, 0, selected.source);
        DrawBorder(0, 0, true);

        var counts = new Dictionary<Direction, int>();
        const int spacing = 0;               // keep 0 to truly overlap
        int step = (area - 1) + spacing;

        foreach (var edge in selected.edges)
        {
            var dir = edge.direction;
            counts[dir] = counts.TryGetValue(dir, out var c) ? c + 1 : 1;

            var v = dir.ToVector(); // EAST=(+1,0), WEST=(-1,0), NORTH=(0,+1), SOUTH=(0,-1)
            int offX = v.x * (counts[dir] * step);
            int offY = v.y * (counts[dir] * step);

            // 1) CHECK validity before drawing
            bool ok = IsOverlapCompatible(selected.source, edge.target, dir, area);

            // 2) Draw the target
            DrawPattern(offY, offX, edge.target);

            // 3) Optional: draw a colored border to indicate validity
            DrawBorder(offY, offX, ok);

            // 4) Log any failures so you can audit counts
            if (!ok)
                Debug.LogWarning($"Invalid overlap: {dir} | N={area} | source#{selected.GetHashCode()} -> target#{edge.target.GetHashCode()}");
        }
    }


    private bool IsOverlapCompatible(Pattern S, Pattern T, Direction dir, int N)
    {
        switch (dir)
        {
            case Direction.EAST:
                for (int y = 0; y < N; y++)
                    for (int x = 1; x < N; x++)
                        if (S[y, x] != T[y, x - 1]) return false;
                return true;

            case Direction.WEST:
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N - 1; x++)
                        if (S[y, x] != T[y, x + 1]) return false;
                return true;

            case Direction.NORTH:
                for (int y = 1; y < N; y++)
                    for (int x = 0; x < N; x++)
                        if (S[y, x] != T[y - 1, x]) return false;
                return true;

            case Direction.SOUTH:
                for (int y = 0; y < N - 1; y++)
                    for (int x = 0; x < N; x++)
                        if (S[y, x] != T[y + 1, x]) return false;
                return true;

            default:
                return false;
        }
    }


}
