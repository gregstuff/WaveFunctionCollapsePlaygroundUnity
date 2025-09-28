using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[ExecuteAlways]
public class OverlapModelDebugTilemapOutput : MonoBehaviour
{
    [SerializeField] private int N;
    [SerializeField] private Tilemap tilemap;
    [SerializeField][Range(0, 1000)] private int outputIndex;
    [SerializeField] private OverlappingModelTileModelSO so;

    private PatternData _selectedPatternData;
    private Dictionary<Direction, List<int>> _directionToCompatible;
    private Dictionary<int, PatternData> _idToPattern;

    // ---------------- Hover Debug ----------------
    [Header("Hover Debug")]
    [SerializeField] private Camera cam;                 // used in Game view; Scene view uses SceneView.camera
    [SerializeField] private bool showBlockOutline = true;
    [SerializeField] private bool enableInSceneView = true;

    private Vector3Int _hoverCell;
    private bool _hoverHasTile;
    private string _hoverText;
    private Rect _hoverRect;
    // ----------------------------------------------------

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
        if (tilemap == null)
        {
            Debug.LogWarning("Tilemap reference is not assigned.");
            return;
        }

        ++outputIndex;
        OnValidate();

        // 1) Visual placement
        tilemap.ClearAllTiles();
        tilemap.RefreshAllTiles(); // ensure a clean redraw in editor paths

        if (_selectedPatternData == null || _selectedPatternData.tilePattern == null)
        {
            Debug.LogWarning("Selected pattern data or tile pattern is null.");
            return;
        }

        OutputPattern(Vector3Int.zero, _selectedPatternData.tilePattern);
        foreach (var dir in DirectionExtensions.Cardinal)
            OutputPatternForDirection(dir);

        tilemap.RefreshAllTiles(); // force repaint once tiles are placed

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
                bool agrees = Agrees(center, cand, dir.ToGridVector(), N);

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
        // Guard: ensure pattern is correctly sized
        if (p == null || p.Length < N * N)
        {
            Debug.LogWarning($"Pattern array size ({p?.Length ?? 0}) < N*N ({N * N}).");
            return;
        }

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
        int[] Top(TileBase[] q) { var r = new int[n]; for (int x = 0; x < n; ++x) r[x] = Tid(q[x + (n - 1) * n]); return r; }
        int[] Bottom(TileBase[] q) { var r = new int[n]; for (int x = 0; x < n; ++x) r[x] = Tid(q[x + 0 * n]); return r; }
        int[] Left(TileBase[] q) { var r = new int[n]; for (int y = 0; y < n; ++y) r[y] = Tid(q[0 + y * n]); return r; }
        int[] Right(TileBase[] q) { var r = new int[n]; for (int y = 0; y < n; ++y) r[y] = Tid(q[(n - 1) + y * n]); return r; }

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

    // ---------------- Hover Logic: GAME VIEW (Play mode) ----------------
    void Update()
    {
        if (!Application.isPlaying) return; // Game view only
        if (tilemap == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        _hoverCell = tilemap.WorldToCell(world);

        _hoverHasTile = tilemap.HasTile(_hoverCell);
        if (!_hoverHasTile)
        {
            _hoverText = null;
            return;
        }

        BuildHoverTextAndRect(_hoverCell, Input.mousePosition);
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return; // only draw this GUI in Game view
        if (!_hoverHasTile || string.IsNullOrEmpty(_hoverText)) return;

        GUI.Box(_hoverRect, GUIContent.none);
        var inner = new Rect(_hoverRect.x + 6, _hoverRect.y + 6, _hoverRect.width - 12, _hoverRect.height - 12);
        GUI.Label(inner, _hoverText);

        if (showBlockOutline)
        {
            int step = Mathf.Max(1, N + 1);
            int bx = Mathf.FloorToInt((float)_hoverCell.x / step);
            int by = Mathf.FloorToInt((float)_hoverCell.y / step);
            var blockMin = new Vector3Int(bx * step, by * step, 0);
            var worldBL = tilemap.CellToWorld(blockMin);
            var worldTR = tilemap.CellToWorld(blockMin + new Vector3Int(N, N, 0));
            DrawWorldRectOutline(worldBL, worldTR, cam);
        }
    }

    private void BuildHoverTextAndRect(Vector3Int cell, Vector3 mousePosScreen)
    {
        int step = Mathf.Max(1, N + 1);
        int bx = Mathf.FloorToInt((float)cell.x / step);
        int by = Mathf.FloorToInt((float)cell.y / step);

        string dirLabel = GetDirectionLabel(bx, by, out Direction? _);
        int rayIndex = Mathf.Max(Mathf.Abs(bx), Mathf.Abs(by));

        int localX = Mod(cell.x, step);
        int localY = Mod(cell.y, step);
        bool inSpacerColumn = (localX == N) || (localY == N);

        _hoverText =
            $"Cell: ({cell.x}, {cell.y})\n" +
            $"Block: ({bx}, {by})\n" +
            $"Direction from origin: {dirLabel}\n" +
            (rayIndex > 0 ? $"Ray index: {rayIndex}\n" : "") +
            $"In-block coords: ({localX}, {localY}){(inSpacerColumn ? "  [spacer]" : "")}";

        _hoverRect = new Rect(mousePosScreen.x + 16, Screen.height - mousePosScreen.y + 16, 320, 90);
    }

    private static int Mod(int a, int m)
    {
        int r = a % m;
        return r < 0 ? r + m : r;
    }

    private string GetDirectionLabel(int bx, int by, out Direction? dir)
    {
        dir = null;
        if (bx == 0 && by == 0) return "ORIGIN";

        if (bx == 0 && by > 0) { dir = Direction.NORTH; return "NORTH"; }
        if (bx == 0 && by < 0) { dir = Direction.SOUTH; return "SOUTH"; }
        if (by == 0 && bx > 0) { dir = Direction.EAST; return "EAST"; }
        if (by == 0 && bx < 0) { dir = Direction.WEST; return "WEST"; }

        // Diagonals aren't expected with current layout, but label them anyway
        string ns = by > 0 ? "N" : "S";
        string ew = bx > 0 ? "E" : "W";
        return ns + ew + " (diagonal)";
    }

    private void DrawWorldRectOutline(Vector3 worldBL, Vector3 worldTR, Camera whichCam)
    {
        if (whichCam == null) return;

        Vector3 worldBR = new Vector3(worldTR.x, worldBL.y, worldBL.z);
        Vector3 worldTL = new Vector3(worldBL.x, worldTR.y, worldBL.z);

        Vector3 sBL = whichCam.WorldToScreenPoint(worldBL);
        Vector3 sBR = whichCam.WorldToScreenPoint(worldBR);
        Vector3 sTR = whichCam.WorldToScreenPoint(worldTR);
        Vector3 sTL = whichCam.WorldToScreenPoint(worldTL);

        sBL.y = Screen.height - sBL.y;
        sBR.y = Screen.height - sBR.y;
        sTR.y = Screen.height - sTR.y;
        sTL.y = Screen.height - sTL.y;

        var prev = GUI.color;
        GUI.color = new Color(1, 1, 1, 0.75f);
        DrawLine(sBL, sBR);
        DrawLine(sBR, sTR);
        DrawLine(sTR, sTL);
        DrawLine(sTL, sBL);
        GUI.color = prev;
    }

    private void DrawLine(Vector3 a, Vector3 b, float width = 1f)
    {
        var delta = (b - a).normalized;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float len = Vector3.Distance(a, b);
        var rect = new Rect(a.x, a.y, len, width);
        var matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.matrix = matrix;
    }

    // ---------------- Hover Logic: SCENE VIEW (Editor) ----------------
#if UNITY_EDITOR
    private void OnEnable()
    {
        UnityEditor.SceneView.duringSceneGui -= OnSceneGUIHover;
        if (enableInSceneView)
            UnityEditor.SceneView.duringSceneGui += OnSceneGUIHover;
    }

    private void OnDisable()
    {
        UnityEditor.SceneView.duringSceneGui -= OnSceneGUIHover;
    }

    private void OnSceneGUIHover(UnityEditor.SceneView sceneView)
    {
        if (!enableInSceneView || tilemap == null) return;

        var e = Event.current;
        if (e == null) return;

        // Update on mouse move and repaint
        if (e.type != EventType.MouseMove && e.type != EventType.Repaint) return;

        var sceneCam = sceneView.camera;
        if (sceneCam == null) return;

        // Robust ray-plane intersection: camera-facing plane through tilemap
        Ray ray = UnityEditor.HandleUtility.GUIPointToWorldRay(e.mousePosition);
        var planeNormal = -sceneCam.transform.forward;
        var plane = new Plane(planeNormal, tilemap.transform.position);

        if (!plane.Raycast(ray, out float t))
        {
            sceneView.Repaint();
            return;
        }

        Vector3 world = ray.origin + ray.direction * t;

        // Which cell?
        Vector3Int cell = tilemap.WorldToCell(world);
        if (!tilemap.HasTile(cell)) { sceneView.Repaint(); return; }

        // Build label text (same logic as Game view)
        int step = Mathf.Max(1, N + 1);
        int bx = Mathf.FloorToInt((float)cell.x / step);
        int by = Mathf.FloorToInt((float)cell.y / step);
        string dirLabel = GetDirectionLabel(bx, by, out Direction? _);
        int rayIndex = Mathf.Max(Mathf.Abs(bx), Mathf.Abs(by));
        int localX = Mod(cell.x, step);
        int localY = Mod(cell.y, step);
        bool inSpacer = (localX == N) || (localY == N);

        string label =
            $"Cell: ({cell.x}, {cell.y})\n" +
            $"Block: ({bx}, {by})\n" +
            $"Direction from origin: {dirLabel}\n" +
            (rayIndex > 0 ? $"Ray index: {rayIndex}\n" : "") +
            $"In-block coords: ({localX}, {localY}){(inSpacer ? "  [spacer]" : "")}";

        // Draw a small floating GUI near the SceneView cursor
        UnityEditor.Handles.BeginGUI();
        {
            var mp = e.mousePosition; // SceneView GUI coords (y-down)
            var rect = new Rect(mp.x + 14, mp.y + 18, 320, 90);
            GUI.Box(rect, GUIContent.none);
            var inner = new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12);
            GUI.Label(inner, label);
        }
        UnityEditor.Handles.EndGUI();

        // Optional block outline in Scene view
        if (showBlockOutline)
        {
            var blc = new Vector3Int(bx * step, by * step, 0);
            Vector3 bl = tilemap.CellToWorld(blc);
            Vector3 tr = tilemap.CellToWorld(blc + new Vector3Int(N, N, 0));
            Vector3 br = new Vector3(tr.x, bl.y, bl.z);
            Vector3 tl = new Vector3(bl.x, tr.y, bl.z);

            UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.75f);
            UnityEditor.Handles.DrawAAPolyLine(3f, new Vector3[] { bl, br, tr, tl, bl });
        }

        // Keep Scene view responsive/updated and avoid selection flicker
        sceneView.Repaint();
        UnityEditor.HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }
#endif
}
