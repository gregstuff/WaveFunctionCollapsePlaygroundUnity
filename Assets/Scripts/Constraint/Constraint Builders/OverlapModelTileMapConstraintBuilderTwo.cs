using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class OverlapModelTileMapConstraintBuilderTwo : ConstraintBuilder
{

    [Header("Object References")]
    [SerializeField] private PopulatedTileMap[] populatedTileMaps;

    [Header("Constraint Builder Controls")]
    [SerializeField] private int N;
    [SerializeField, Range(1, 8)] private int symmetry = 8;

    private List<TileBase[]> _patterns;
    private List<double> _weights;
    private Dictionary<Direction, Dictionary<int, List<int>>> _propagator;

    [ContextMenu("Generate Constraints")]
    public override void GenerateConstraints()
    {
        if (N < 2) throw new ArgumentException("N must be at least 2x2");

        _patterns = new();
        _weights = new();
        _propagator = new();

        foreach (var tilemap in populatedTileMaps)
        {
            ExtractPatternData(tilemap);
        }
        ExportOverlapModelAsset();
    }

    private TileBase[] Pattern(Func<int, int, TileBase> f, int N)
    {
        TileBase[] tiles = new TileBase[N * N];
        for (int y = 0; y < N; ++y) for (int x = 0; x < N; ++x) tiles[x + y * N] = f(x, y);
        return tiles;
    }

    private TileBase[] Rotate(TileBase[] p, int N) => Pattern((x, y) => p[N - 1 - y + x * N], N);
    private TileBase[] Reflect(TileBase[] p, int N) => Pattern((x, y) => p[N - 1 - x + y * N], N);

    private bool Agrees(TileBase[] p1, TileBase[] p2, Vector2Int dir, int N)
    {
        int dx = dir.x, dy = dir.y;
        int xmin = dx < 0 ? 0 : dx;
        int xmax = dx < 0 ? dx + N : N;
        int ymin = dy < 0 ? 0 : dy;
        int ymax = dy < 0 ? dy + N : N;
        for (int y = ymin; y < ymax; ++y) for (int x = xmin; x < xmax; ++x) if (p1[x + N * y] != p2[x - dx + N * (y - dy)]) return false;
        return true;
    }

    private static long Hash(TileBase[] p)
    {
        unchecked
        {
            ulong h = 1469598103934665603UL; // FNV-1a 64-bit
            for (int i = 0; i < p.Length; i++)
            {
                var tile = p[i];
                // Prefer GUID; fallback to name if needed
                string guid = null;
#if UNITY_EDITOR
                var path = AssetDatabase.GetAssetPath(tile);
                if (!string.IsNullOrEmpty(path)) guid = AssetDatabase.AssetPathToGUID(path);
#endif
                var key = guid ?? tile?.name ?? "<null>";
                foreach (char c in key) { h ^= (byte)c; h *= 1099511628211UL; }
                // separator to avoid “ab|c” == “a|bc” when concatenating strings across cells
                h ^= 0xFF; h *= 1099511628211UL;
            }
            return (long)h;
        }
    }

    private void ExtractPatternData(PopulatedTileMap tilemap)
    {
        var (height, width, tiles, uniqueTiles) = tilemap.GetFlatTiles();

        Dictionary<long, int> patternIndices = new();
        List<double> localWeights = new();
        List<TileBase[]> localPatterns = new();

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                TileBase[][] ts = new TileBase[8][];
                ts[0] = Pattern((dx, dy) => tiles[(x + dx) % width + (y + dy) % height * width], N);
                ts[1] = Reflect(ts[0], N);
                ts[2] = Rotate(ts[0], N);
                ts[3] = Reflect(ts[2], N);
                ts[4] = Rotate(ts[2], N);
                ts[5] = Reflect(ts[4], N);
                ts[6] = Rotate(ts[4], N);
                ts[7] = Reflect(ts[6], N);

                for (int k = 0; k < symmetry && k < 8; ++k)
                {
                    TileBase[] p = ts[k];
                    long h = Hash(p);
                    if (patternIndices.TryGetValue(h, out int index)) localWeights[index] = localWeights[index] + 1;
                    else
                    {
                        patternIndices.Add(h, localWeights.Count);
                        localWeights.Add(1.0);
                        localPatterns.Add(p);
                    }
                }
            }
        }
        int T = localWeights.Count;
        Dictionary<Direction, Dictionary<int, List<int>>> localPropagator
            = new Dictionary<Direction, Dictionary<int, List<int>>>();

        foreach (Direction dir in DirectionExtensions.Cardinal)
        {
            localPropagator[dir] = new Dictionary<int, List<int>>();
            var dirVector = dir.ToVector();
            for (int t = 0; t < T; ++t)
            {
                List<int> compatiblePatterns = new();
                for (int t2 = 0; t2 < T; ++t2)
                    if (Agrees(
                        localPatterns[t],
                        localPatterns[t2],
                        dirVector,
                        N)) compatiblePatterns.Add(t2);
                localPropagator[dir][t] = compatiblePatterns;
            }
        }
        MergePatternData(localPatterns, localWeights, localPropagator);
    }

    private void MergePatternData(List<TileBase[]> newPatterns,
        List<double> newWeights, Dictionary<Direction, Dictionary<int, List<int>>> newPropagator)
    {
        if (_patterns.Count == 0)
        {
            _patterns.AddRange(newPatterns);
            _weights.AddRange(newWeights);
            _propagator = newPropagator;
            return;
        }

        Dictionary<int, int> newToMergedIndexMap = new();

        for (int i = 0; i < newPatterns.Count; ++i)
        {
            long hash = Hash(newPatterns[i]);
            int existingIndex = -1;
            for (int j = 0; j < _patterns.Count; ++j)
            {
                if (Hash(_patterns[j]) == hash)
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                _weights[existingIndex] += newWeights[i];
                newToMergedIndexMap[i] = existingIndex;
            }
            else
            {
                int newIndex = _patterns.Count;
                newToMergedIndexMap[i] = newIndex;
                _patterns.Add(newPatterns[i]);
                _weights.Add(newWeights[i]);
            }
        }

        foreach (Direction dir in DirectionExtensions.Cardinal)
        {
            if (!newPropagator.ContainsKey(dir))
                continue;

            if (!_propagator.ContainsKey(dir))
                _propagator[dir] = new Dictionary<int, List<int>>();

            foreach (var kvp in newPropagator[dir])
            {
                int sourcePattern = kvp.Key;
                if (!newToMergedIndexMap.TryGetValue(sourcePattern, out int mergedSourceIndex))
                    continue;

                if (!_propagator[dir].ContainsKey(mergedSourceIndex))
                    _propagator[dir][mergedSourceIndex] = new List<int>();

                var mergedCompatibleList = _propagator[dir][mergedSourceIndex];

                foreach (int targetPattern in kvp.Value)
                {
                    if (newToMergedIndexMap.TryGetValue(targetPattern, out int mergedTargetIndex))
                    {
                        if (!mergedCompatibleList.Contains(mergedTargetIndex))
                            mergedCompatibleList.Add(mergedTargetIndex);
                    }
                }
            }
        }
    }

    private void ExportOverlapModelAsset()
    {
        var so = ScriptableObject.CreateInstance<OverlappingModelTileModelSO>();
        so.N = N;
        so.patternSize = N;

        for (int i = 0; i < _patterns.Count; i++)
        {
            so.patterns.Add(new OverlappingModelTileModelSO.PatternData
            {
                patternId = i,
                tiles = new List<TileBase>(_patterns[i]),
                weight = _weights[i]
            });
        }

        foreach (var dirKv in _propagator)
        {
            Direction direction = dirKv.Key;
            var patternAdjacencies = dirKv.Value;

            foreach (var patternKv in patternAdjacencies)
            {
                int sourcePatternId = patternKv.Key;
                List<int> compatiblePatternIds = patternKv.Value;

                var adjacency = new OverlappingModelTileModelSO.PatternAdjacency
                {
                    sourcePatternId = sourcePatternId,
                    direction = direction,
                    compatiblePatternIds = new List<int>(compatiblePatternIds) // Copy the list
                };

                so.adjacencies.Add(adjacency);
            }
        }

        var defaultName = $"OverlapModel_N{N}.asset";
        var path = EditorUtility.SaveFilePanelInProject(
            "Save Overlap Model",
            defaultName,
            "asset",
            "Choose where to save the learned overlap model");

        if (string.IsNullOrEmpty(path))
        {
            DestroyImmediate(so);
            return;
        }

        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(so);

        Debug.Log($"Overlap model saved: {path} | Patterns: {_patterns.Count}, Adjacency rules: {so.adjacencies.Count}");
    }

}
