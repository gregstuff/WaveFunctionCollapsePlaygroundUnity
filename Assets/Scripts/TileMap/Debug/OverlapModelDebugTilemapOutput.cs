using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class OverlapModelDebugTilemapOutput : MonoBehaviour
{
    [SerializeField] private int N;
    [SerializeField] private Tilemap tilemap;
    [SerializeField][Range(0, 1000)] private int outputIndex;
    [SerializeField] private OverlappingModelTileModelSO so;

    private PatternData _selectedPatternData;
    private Dictionary<Direction, List<int>> _directionToCompatible;
    private Dictionary<int, PatternData> _idToPattern;



    void OnValidate()
    {
        if (so == null || so.patterns == null || so.patterns.Count == 0) return;
        if (outputIndex > so.patterns.Count - 1) throw new System.Exception("Index out of range!");

        _selectedPatternData = so.patterns[outputIndex];
        _directionToCompatible = new();
        _idToPattern = new();
        N = so.N;

        foreach (var p in so.patterns) _idToPattern[p.patternId] = p;

        foreach (var adj in so.adjacencies)
            if (adj.sourcePatternId == _selectedPatternData.patternId)
                _directionToCompatible[adj.direction] = adj.compatiblePatternIds;
    }

    [ContextMenu("Debug: Visualize + Dump")]
    public void VisualizeAndDump()
    {

        // 1) Visual placement
        tilemap.ClearAllTiles();
        OutputPattern(Vector3Int.zero, _selectedPatternData.tilePattern);
        foreach (var dir in DirectionExtensions.Cardinal)
            OutputPatternForDirection(dir);

        // 2) One consolidated console log
        var center = _selectedPatternData.tilePattern;
        var log = new StringBuilder(16_000);
        log.AppendLine($"[WFC DEBUG] N={N}  centerId={_selectedPatternData.patternId}  weight={_selectedPatternData.weight}");
        log.AppendLine("CENTER GRID (top->down):");
        log.AppendLine(FormatGrid(center, N)).AppendLine();

        foreach (var dir in DirectionExtensions.Cardinal)
        {
            if (!_directionToCompatible.TryGetValue(dir, out var ids) || ids == null || ids.Count == 0)
            {
                log.AppendLine($"[{dir}] no compatibles.");
                continue;
            }

            int idx = 0;
            foreach (var pid in ids)
            {
                var cand = _idToPattern[pid].tilePattern;

                // edge signatures & checks
                var (edgeA, edgeB) = GetMatchingEdges(center, cand, dir, N);
                bool edgesMatch = EdgesEqual(edgeA, edgeB);
                bool agrees = Agrees(center, cand, dir.ToArrayVector(), N);

                log.AppendLine($"[{dir}] #{++idx} center:{_selectedPatternData.patternId}  cand:{pid}  edgesMatch={edgesMatch}  agrees={agrees}");
                log.AppendLine("  CENTER GRID:");
                Indent(log, FormatGrid(center, N));
                log.AppendLine("  CAND GRID:");
                Indent(log, FormatGrid(cand, N));
                log.AppendLine($"  Compared edges for {dir}:");
                log.AppendLine($"    center {dir} -> {FormatEdge(edgeA)}");
                log.AppendLine($"    cand   {dir.GetOpposite()} -> {FormatEdge(edgeB)}");
                log.AppendLine();
            }
        }

        Debug.Log(log.ToString());
    }

    // ----- visual helpers -----
    private void OutputPatternForDirection(Direction dir)
    {
        if (!_directionToCompatible.TryGetValue(dir, out var ids)) return;

        int index = 1;
        foreach (var pid in ids)
        {
            var patt = _idToPattern[pid].tilePattern;
            var v = dir.ToGridVector();
            var d = new Vector2Int(v.x * (N + 1) * index, v.y * (N + 1) * index);
            OutputPattern(new Vector3Int(d.x, d.y, 0), patt);
            ++index;
        }
    }

    private void OutputPattern(Vector3Int pos, TileBase[] p)
    {
        for (int y = 0; y < N; ++y)
            for (int x = 0; x < N; ++x)
                tilemap.SetTile(new Vector3Int(x + pos.x, y + pos.y, 0), p[x + y * N]);
    }

    // ----- console helpers -----
    private static int Tid(TileBase t) => t ? t.GetInstanceID() : 0;

    private static void Indent(StringBuilder sb, string text)
    {
        using var sr = new System.IO.StringReader(text);
        string? line;
        while ((line = sr.ReadLine()) != null) sb.Append("    ").AppendLine(line);
    }

    private string FormatGrid(TileBase[] p, int n)
    {
        var sb = new StringBuilder(n * n * 6);
        for (int y = n - 1; y >= 0; --y) // print top row first (visual-friendly)
        {
            for (int x = 0; x < n; ++x)
            {
                int id = Tid(p[x + y * n]) & 0xFFFF; // shorten for readability
                sb.Append(id.ToString("X4"));
                if (x < n - 1) sb.Append(' ');
            }
            if (y > 0) sb.AppendLine();
        }
        return sb.ToString();
    }

    private (int[] a, int[] b) GetMatchingEdges(TileBase[] center, TileBase[] cand, Direction dir, int n)
    {
        int[] Top(TileBase[] p) { var r = new int[n]; for (int x = 0; x < n; ++x) r[x] = Tid(p[x + (n - 1) * n]); return r; }
        int[] Bottom(TileBase[] p) { var r = new int[n]; for (int x = 0; x < n; ++x) r[x] = Tid(p[x + 0 * n]); return r; }
        int[] Left(TileBase[] p) { var r = new int[n]; for (int y = 0; y < n; ++y) r[y] = Tid(p[0 + y * n]); return r; }
        int[] Right(TileBase[] p) { var r = new int[n]; for (int y = 0; y < n; ++y) r[y] = Tid(p[(n - 1) + y * n]); return r; }

        return dir switch
        {
            Direction.NORTH => (Top(center), Bottom(cand)),
            Direction.SOUTH => (Bottom(center), Top(cand)),
            Direction.EAST => (Right(center), Left(cand)),
            Direction.WEST => (Left(center), Right(cand)),
            _ => (System.Array.Empty<int>(), System.Array.Empty<int>())
        };
    }

    private static bool EdgesEqual(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // local copy of your agrees so the dump can call it
    private bool Agrees(TileBase[] p1, TileBase[] p2, Vector2Int dir, int n)
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

    private static string FormatEdge(int[] e)
    {
        var sb = new StringBuilder(e.Length * 5);
        for (int i = 0; i < e.Length; i++)
        {
            sb.Append((e[i] & 0xFFFF).ToString("X4"));
            if (i < e.Length - 1) sb.Append(' ');
        }
        return sb.ToString();
    }
}
