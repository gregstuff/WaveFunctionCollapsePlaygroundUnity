using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Assert = NUnit.Framework.Assert;

public class WfcOverlapModelTests
{
    // Helper: build a 2x2 "solid color" pattern (all cells use the same tile)
    private static OverlappingModelTileModelSO.PatternFrequency MakeSolidPattern(TileBase tile, int count)
    {
        // N is 2 in this test, so Tiles length = 4, in row-major: [ (0,0), (0,1), (1,0), (1,1) ]
        var tiles = new TileBase[4] { tile, tile, tile, tile };
        var pat = new Pattern(2, 2, tiles);
        return new OverlappingModelTileModelSO.PatternFrequency { pattern = pat, count = count };
    }

    // Helper: add 4-direction self-compatibility for a pattern
    private static OverlappingModelTileModelSO.PatternAdjacency MakeSelfAdjacencies(Pattern p)
    {
        var pa = new OverlappingModelTileModelSO.PatternAdjacency { source = p };
        pa.edges.Add(new OverlappingModelTileModelSO.DirectionAdjacency { direction = Direction.NORTH, target = p, count = 1 });
        pa.edges.Add(new OverlappingModelTileModelSO.DirectionAdjacency { direction = Direction.SOUTH, target = p, count = 1 });
        pa.edges.Add(new OverlappingModelTileModelSO.DirectionAdjacency { direction = Direction.EAST, target = p, count = 1 });
        pa.edges.Add(new OverlappingModelTileModelSO.DirectionAdjacency { direction = Direction.WEST, target = p, count = 1 });
        return pa;
    }

    [Test]
    public void OverlapModel_WFC_Completes_NoContradictions_And_RespectsAdjacency()
    {
        // --- Arrange --------------------------------------------------------
        // Create two distinct tiles for red/green (in-memory)
        var redTile = ScriptableObject.CreateInstance<Tile>();
        var greenTile = ScriptableObject.CreateInstance<Tile>();
        redTile.name = "RedTile";
        greenTile.name = "GreenTile";

        // Create ScriptableObject model
        var model = ScriptableObject.CreateInstance<OverlappingModelTileModelSO>();
        model.N = 2;

        // Two uniform patterns, equal weights
        var redPF = MakeSolidPattern(redTile, count: 1);
        var greenPF = MakeSolidPattern(greenTile, count: 1);
        model.patterns = new List<OverlappingModelTileModelSO.PatternFrequency> { redPF, greenPF };

        // Compatibilities: only allow like-with-like (R next to R, G next to G)
        model.compatibilities = new List<OverlappingModelTileModelSO.PatternAdjacency>
        {
            MakeSelfAdjacencies(redPF.pattern),
            MakeSelfAdjacencies(greenPF.pattern)
        };

        // Init a small grid (e.g., 3x3)
        var dims = new Vector2Int(3, 3);
        model.Init(dims);

        // This mimics WaveFunctionCollapse's main loop (without coroutine/tilemap)
        var candidates = new Queue<Cell>();
        int collapsedCount = 0;
        TileBase? chosenFirst = null;

        // --- Act ------------------------------------------------------------
        while (true)
        {
            candidates.Clear();
            var target = model.GetNext();
            if (target == null) break; // finished

            // Collapse target
            var selectedTile = model.CollapseCell(target.Pos);
            collapsedCount++;

            // Track the first selection; later selections must match it (all cells uniform)
            chosenFirst ??= selectedTile;
            Assert.AreEqual(chosenFirst, selectedTile,
                "Adjacency should enforce a uniform tiling: subsequent collapses must match the first tile.");

            // Propagate like WaveFunctionCollapse does
            model.EnqueueNeighbours(target, candidates);

            while (candidates.Count > 0)
            {
                var cand = candidates.Dequeue();
                cand.InQueue = false;

                var entropy = model.ReduceByNeighbors(cand);

                // If possibilities changed, enqueue its neighbours to continue propagation
                if (entropy.HasDiff())
                    model.EnqueueNeighbours(cand, candidates);

                // NoEntropy() means 0 possibilities ⇒ contradiction
                Assert.IsFalse(entropy.NoEntropy(), $"Contradiction at {cand.Pos}");
            }
        }

        // --- Assert ---------------------------------------------------------
        int totalCells = dims.x * dims.y;
        Assert.AreEqual(totalCells, collapsedCount, "Every cell should be collapsed exactly once.");
        Assert.IsNotNull(chosenFirst, "At least one cell should have collapsed.");
        // If we reached here, no contradictions occurred and all collapses agreed on a single pattern.
    }
}
