
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class OverlapModelTileMapConstraintBuilder : ConstraintBuilder
{

    [Header("Object References")]
    [SerializeField] private PopulatedTileMap[] populatedTileMaps;

    [Header("Constraint Builder Controls")]
    [SerializeField] private int areaWidth;

    private Dictionary<Pattern, int> _patternCounts;
    private Dictionary<Pattern, Dictionary<Direction, Dictionary<Pattern, int>>> _allowedPatternsForDirection;

    [ContextMenu("Generate Constraints")]
    public override void GenerateConstraints()
    {
        _patternCounts = new();
        _allowedPatternsForDirection = new();

        if (areaWidth < 2) throw new ArgumentException("Area width must be at least 2x2");

        foreach (var tilemap in populatedTileMaps)
        {
            var tiles = tilemap.GetTiles();
            ExtractPatternData(tiles);
        }

        ExportOverlapModelAsset();

    }

    private void ExtractPatternData(TileBase[,] tiles)
    {
        int height = tiles.GetLength(0);
        int width = tiles.GetLength(1);

        for (int y = 0; y <= height - areaWidth; ++y)
        {
            for (int x = 0; x <= width - areaWidth; ++x)
            {
                var flatPattern = ExtractFlatPattern(y, x, height, width, tiles);

                Pattern cellPattern = new Pattern(areaWidth, areaWidth, flatPattern);
                Pattern eastPattern = null;
                Pattern westPattern = null;
                Pattern southPattern = null;
                Pattern northPattern = null;

                if (x + 1 <= width - areaWidth)
                {
                    var eastFlatPattern = ExtractFlatPattern(y, x + 1, height, width, tiles);
                    eastPattern = new Pattern(areaWidth, areaWidth, eastFlatPattern);
                }

                if (x - 1 >= 0)
                {
                    var westFlatPattern = ExtractFlatPattern(y, x - 1, height, width, tiles);
                    westPattern = new Pattern(areaWidth, areaWidth, westFlatPattern);
                }

                if (y + 1 <= height - areaWidth)
                {
                    var southFlatPattern = ExtractFlatPattern(y + 1, x, height, width, tiles);
                    southPattern = new Pattern(areaWidth, areaWidth, southFlatPattern);
                }

                if (y - 1 >= 0)
                {
                    var northFlatPattern = ExtractFlatPattern(y - 1, x, height, width, tiles);
                    northPattern = new Pattern(areaWidth, areaWidth, northFlatPattern);
                }

                RecordPatterns(cellPattern, eastPattern, westPattern, southPattern, northPattern);
                RecordPatternAdjacencies(cellPattern, eastPattern, westPattern, southPattern, northPattern);

            }
        }

    }

    private void RecordPatternAdjacencies(Pattern curr, Pattern east, Pattern west, Pattern south, Pattern north)
    {
        void AddPatternForDirection(Dictionary<Direction, Dictionary<Pattern, int>> directionsForPattern, Direction dir, Pattern p)
        {
            if (p == null) return;

            if (!directionsForPattern.TryGetValue(dir, out var patternsForDir))
            {
                directionsForPattern[dir] = patternsForDir = new();
            }

            if (!patternsForDir.TryGetValue(p, out var _))
            {
                patternsForDir[p] = 0;
            }

            patternsForDir[p] = patternsForDir[p] + 1;
        }

        if (!_allowedPatternsForDirection.TryGetValue(curr, out var directionsForPattern))
        {
            _allowedPatternsForDirection[curr] = new();
            directionsForPattern = _allowedPatternsForDirection[curr];
        }

        AddPatternForDirection(directionsForPattern, Direction.EAST, east);
        AddPatternForDirection(directionsForPattern, Direction.WEST, west);
        AddPatternForDirection(directionsForPattern, Direction.SOUTH, south);
        AddPatternForDirection(directionsForPattern, Direction.NORTH, north);
    }

    private void RecordPatterns(params Pattern[] patterns)
    {
        foreach (var p in patterns)
        {
            if (p == null) continue;

            if (!_patternCounts.TryGetValue(p, out var _)) _patternCounts.Add(p, 0);

            _patternCounts[p] = _patternCounts[p] + 1;
        }
    }

    private TileBase[] ExtractFlatPattern(int y, int x, int height, int width, TileBase[,] tiles)
    {
        var areaTilesBuffer = new TileBase[areaWidth * areaWidth];
        for (int ay = 0; ay < areaWidth; ++ay)
        {
            for (int ax = 0; ax < areaWidth; ++ax)
            {
                int resolvedY = y + ay;
                int resolvedX = x + ax;

                if (resolvedY >= height || resolvedX >= width) throw new System.Exception("Pattern is outside bounds!");

                areaTilesBuffer[ay * areaWidth + ax] = tiles[resolvedY, resolvedX];
            }
        }

        return areaTilesBuffer;
    }

    private void ExportOverlapModelAsset()
    {
        var so = ScriptableObject.CreateInstance<OverlappingModelTileModelSO>();
        so.N = areaWidth;

        foreach (var kv in _patternCounts)
        {
            if (kv.Key == null) continue;
            so.patterns.Add(new OverlappingModelTileModelSO.PatternFrequency
            {
                pattern = kv.Key,
                count = kv.Value
            });
        }

        foreach (var srcKv in _allowedPatternsForDirection)
        {
            var pa = new OverlappingModelTileModelSO.PatternAdjacency
            {
                source = srcKv.Key
            };

            var dirMap = srcKv.Value;
            foreach (var dirKv in dirMap)
            {
                var dir = dirKv.Key;
                var targets = dirKv.Value;
                foreach (var tgtKv in targets)
                {
                    if (tgtKv.Key == null) continue;
                    pa.edges.Add(new OverlappingModelTileModelSO.DirectionAdjacency
                    {
                        direction = dir,
                        target = tgtKv.Key,
                        count = tgtKv.Value
                    });
                }
            }

            so.compatibilities.Add(pa);
        }

        var defaultName = $"OverlapModel_N{areaWidth}.asset";
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
        Debug.Log($"Overlap model saved: {path} | patterns={so.patterns.Count}, adjacency entries={so.compatibilities.Count}");
    }

}
