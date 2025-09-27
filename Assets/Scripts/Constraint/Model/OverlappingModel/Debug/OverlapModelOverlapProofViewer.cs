using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class OverlapModelOverlapProofViewer : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private OverlappingModelTileModelSO model;
    [SerializeField] private int centerPatternId = 0;       // pattern to inspect
    [SerializeField] private Direction direction = Direction.NORTH;
    [SerializeField] private int candidateIndex = 0;         // index within the compatible list for that direction

    [Header("Tilemaps")]
    [SerializeField] private Tilemap baseTilemap;            // drawn opaque
    [SerializeField] private Tilemap overlayTilemap;         // drawn above, semi-transparent
    [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.45f;

    [Header("Colors")]
    [SerializeField] private Color anchorColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    [SerializeField] private Color okColor = new Color(0.2f, 1f, 0.2f, 0.9f);
    [SerializeField] private Color badColor = new Color(1f, 0.2f, 0.2f, 0.9f);

    // cached
    private Dictionary<int, PatternData> _idToPattern;
    private Dictionary<Direction, List<int>> _compatByDir;
    private int _n;

    void OnValidate()
    {
        if (model == null || model.patterns == null || model.patterns.Count == 0) return;

        _n = model.N;

        _idToPattern ??= new Dictionary<int, PatternData>();
        _idToPattern.Clear();
        foreach (var p in model.patterns) _idToPattern[p.patternId] = p;

        // build compatibility map for the chosen center pattern
        _compatByDir = new Dictionary<Direction, List<int>>();
        foreach (var adj in model.adjacencies)
        {
            if (adj.sourcePatternId != centerPatternId) continue;
            _compatByDir[adj.direction] = adj.compatiblePatternIds;
        }

        centerPatternId = Mathf.Clamp(centerPatternId, 0, model.patterns.Count - 1);
        if (!_compatByDir.ContainsKey(direction)) candidateIndex = 0;
        else candidateIndex = Mathf.Clamp(candidateIndex, 0, Mathf.Max(0, _compatByDir[direction].Count - 1));
    }

    [ContextMenu("Render Overlap Proof")]
    public void RenderOverlapProof()
    {
        if (model == null || baseTilemap == null || overlayTilemap == null)
        {
            Debug.LogWarning("Assign model, baseTilemap, and overlayTilemap.");
            return;
        }

        OnValidate(); // refresh caches

        baseTilemap.ClearAllTiles();
        overlayTilemap.ClearAllTiles();
        overlayTilemap.color = new Color(1f, 1f, 1f, overlayAlpha);

        if (!_idToPattern.TryGetValue(centerPatternId, out var centerPd))
        {
            Debug.LogWarning($"Center pattern {centerPatternId} not found.");
            return;
        }

        // Place center at origin
        var origin = Vector3Int.zero;
        PlacePattern(baseTilemap, origin, centerPd.tilePattern, _n);

        // mark anchor (0,0)
        var anchor = new Vector3Int(0, 0, 0);
        baseTilemap.SetTileFlags(anchor, TileFlags.None);
        baseTilemap.SetColor(anchor, anchorColor);

        // pick candidate for chosen direction (first compatible by default)
        if (!_compatByDir.TryGetValue(direction, out var ids) || ids.Count == 0)
        {
            Debug.Log($"[{direction}] No compatible patterns for center={centerPatternId}.");
            return;
        }
        int candId = ids[Mathf.Clamp(candidateIndex, 0, ids.Count - 1)];
        var candPd = _idToPattern[candId];

        // Place candidate with a 1-cell offset in the chosen direction (grid space, y-up)
        var v = direction.ToGridVector();
        var disp = new Vector3Int(v.x, v.y, 0); // EXACTLY 1 cell
        PlacePattern(overlayTilemap, disp, candPd.tilePattern, _n);

        // Tint the overlap band on the base map to prove equality the way Agrees() checks it
        HighlightOverlapStrip(direction, origin, disp, centerPd.tilePattern, candPd.tilePattern, _n);

        // Optional: log a quick verdict
        bool agrees = Agrees(centerPd.tilePattern, candPd.tilePattern, direction.ToArrayVector(), _n);
        Debug.Log($"[Overlap Proof] center={centerPatternId}, cand={candId}, dir={direction}, agrees={agrees}");
    }

    // ---------- helpers ----------

    private static void PlacePattern(Tilemap map, Vector3Int pos, TileBase[] p, int n)
    {
        for (int y = 0; y < n; ++y)
            for (int x = 0; x < n; ++x)
                map.SetTile(new Vector3Int(pos.x + x, pos.y + y, 0), p[x + y * n]);
    }

    private static int Tid(TileBase t) => t ? t.GetInstanceID() : 0;

    private void HighlightOverlapStrip(Direction dir, Vector3Int basePos, Vector3Int candPos,
                                       TileBase[] center, TileBase[] candidate, int n)
    {
        void Tint(Vector3Int cell, bool match)
        {
            baseTilemap.SetTileFlags(cell, TileFlags.None);
            baseTilemap.SetColor(cell, match ? okColor : badColor);
        }

        switch (dir)
        {
            case Direction.NORTH: // center rows 0..n-2 vs cand rows 1..n-1
                for (int y = 0; y <= n - 2; ++y)
                    for (int x = 0; x < n; ++x)
                    {
                        var a = center[x + y * n];
                        var b = candidate[x + (y + 1) * n];
                        var cell = new Vector3Int(basePos.x + x, basePos.y + y, 0);
                        Tint(cell, Tid(a) == Tid(b));
                    }
                break;

            case Direction.SOUTH: // center rows 1..n-1 vs cand rows 0..n-2
                for (int y = 1; y < n; ++y)
                    for (int x = 0; x < n; ++x)
                    {
                        var a = center[x + y * n];
                        var b = candidate[x + (y - 1) * n];
                        var cell = new Vector3Int(basePos.x + x, basePos.y + y, 0);
                        Tint(cell, Tid(a) == Tid(b));
                    }
                break;

            case Direction.EAST: // center cols 1..n-1 vs cand cols 0..n-2
                for (int x = 1; x < n; ++x)
                    for (int y = 0; y < n; ++y)
                    {
                        var a = center[x + y * n];
                        var b = candidate[(x - 1) + y * n];
                        var cell = new Vector3Int(basePos.x + x, basePos.y + y, 0);
                        Tint(cell, Tid(a) == Tid(b));
                    }
                break;

            case Direction.WEST: // center cols 0..n-2 vs cand cols 1..n-1
                for (int x = 0; x <= n - 2; ++x)
                    for (int y = 0; y < n; ++y)
                    {
                        var a = center[x + y * n];
                        var b = candidate[(x + 1) + y * n];
                        var cell = new Vector3Int(basePos.x + x, basePos.y + y, 0);
                        Tint(cell, Tid(a) == Tid(b));
                    }
                break;
        }
    }

    // local copy of your Agrees (array-space: y-down)
    private static bool Agrees(TileBase[] p1, TileBase[] p2, Vector2Int dir, int n)
    {
        int dx = dir.x, dy = dir.y;
        int xmin = dx < 0 ? 0 : dx;
        int xmax = dx < 0 ? dx + n : n;
        int ymin = dy < 0 ? 0 : dy;
        int ymax = dy < 0 ? dy + n : n;
        for (int y = ymin; y < ymax; ++y)
            for (int x = xmin; x < xmax; ++x)
                if (p1[x + n * y] != p2[x - dx + n * (y - dy)]) return false;
        return true;
    }
}
