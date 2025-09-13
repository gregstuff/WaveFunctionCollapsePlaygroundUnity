using System;
using System.Collections.Generic;
using System.Linq; // kept because you use .First() elsewhere
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "ProcGen/WFC/Overlap Model")]
public class OverlappingModelTileModelSO : ConstraintModelSO
{
    [SerializeField] public int N;
    [SerializeField] public List<PatternFrequency> patterns;
    [SerializeField] public List<PatternAdjacency> compatibilities;

    private Dictionary<Vector2Int, Cell> _positionToCell;
    private Dictionary<Vector2Int, HashSet<Pattern>> _positionsToPossiblePatterns;
    private Dictionary<Vector2Int, Dictionary<Direction, Cell>> _neighboursForPos;
    private Dictionary<Pattern, Dictionary<Direction, HashSet<Pattern>>> _patternToDirectionsToAllowedPatterns;

    private Dictionary<Pattern, int> _weights;
    private Dictionary<Pattern, double> _weightLogs;

    private Vector2Int _dimensions;

    private List<HashSet<Cell>> _entropyBuckets;

    private HashSet<Pattern> _unionSet;

    private System.Random _random = new System.Random();

    [Serializable]
    public class PatternFrequency
    {
        public Pattern pattern;
        public int count;
    }

    [Serializable]
    public class DirectionAdjacency
    {
        public Direction direction;
        public Pattern target;
        public int count;
    }

    [Serializable]
    public class PatternAdjacency
    {
        public Pattern source;
        public List<DirectionAdjacency> edges = new();
    }

    public OverlappingModelTileModelSO()
    {
        patterns = new();
        compatibilities = new();
        _unionSet = new();
    }

    public override TileBase CollapseCell(Vector2Int pos)
    {
        var cell = _positionToCell[pos];
        var possibilities = _positionsToPossiblePatterns[pos];

        // no need to add cell again for entropy tracking
        _entropyBuckets[possibilities.Count - 1].Remove(cell);

        // ==== CHANGE #1 (perf): replace LINQ + list ranges with a single-pass weighted choice (no allocs) ====
        int totalWeight = 0;
        foreach (var p in possibilities)
            totalWeight += _weights[p];

        int r = _random.Next(totalWeight);
        Pattern selectedPossibility = null;
        foreach (var p in possibilities)
        {
            int w = _weights[p];
            if (r < w) { selectedPossibility = p; break; }
            r -= w;
        }
        // ==== END CHANGE #1 ====

        cell.Collapse();

        _positionsToPossiblePatterns[pos].Clear();
        _positionsToPossiblePatterns[pos].Add(selectedPossibility);

        // top left tile
        return selectedPossibility.Tiles[0];
    }

    public override void EnqueueNeighbours(Cell cell, Queue<Cell> candidates)
    {
        var neighboursMap = _neighboursForPos[cell.Pos];
        foreach (var kvp in neighboursMap)
        {
            var neighbour = kvp.Value;
            // NOTE: left unchanged to avoid altering external queue semantics.
            if (neighbour != null && !neighbour.Collapsed && !candidates.Contains(neighbour)) candidates.Enqueue(neighbour);
        }
    }

    public override Cell GetNext()
    {
        // Buckets are indexed by (count - 1):
        // 0: collapsed (count == 1)  -> skip
        // 1: two options              -> start here
        // 2+: higher entropies
        for (int k = 1; k < _entropyBuckets.Count; k++)   // was 2
        {
            Cell best = null;
            double bestH = double.PositiveInfinity;

            foreach (var cell in _entropyBuckets[k])
            {
                if (cell.Collapsed) continue;
                double h = ComputeEntropy(cell);
                if (h < bestH)
                {
                    bestH = h;
                    best = cell;
                }
            }

            if (best != null) return best;
        }

        // Fallback: if buckets got out of sync, pick any non-collapsed cell.
        foreach (var kv in _positionToCell)
            if (!kv.Value.Collapsed) return kv.Value;

        return null; // all done
    }


    public override void Init(Vector2Int dimensions)
    {
        _weights = new();
        _weightLogs = new();
        _dimensions = dimensions;

        InitPatternsWeights(out var possiblePatterns);
        InitEntropyBuckets();
        InitAdjacentPatterns();
        InitGrid(possiblePatterns);
    }

    private void InitPatternsWeights(out List<Pattern> possiblePatterns)
    {
        possiblePatterns = new List<Pattern>();

        for (int i = 0; i < patterns.Count; i++)
        {
            int weight = Mathf.Max(1, patterns[i].count);
            _weights[patterns[i].pattern] = weight;
            _weightLogs[patterns[i].pattern] = weight * Math.Log(weight);

            possiblePatterns.Add(patterns[i].pattern);
        }
    }

    private void InitAdjacentPatterns()
    {
        _patternToDirectionsToAllowedPatterns = new();

        foreach (var compatibility in compatibilities)
        {
            _patternToDirectionsToAllowedPatterns[compatibility.source] = new();

            foreach (var edge in compatibility.edges)
            {
                if (!_patternToDirectionsToAllowedPatterns[compatibility.source].TryGetValue(edge.direction, out var patterns))
                {
                    patterns = new HashSet<Pattern>();
                    _patternToDirectionsToAllowedPatterns[compatibility.source][edge.direction] = patterns;
                }
                patterns.Add(edge.target);
            }
        }
    }

    private void InitEntropyBuckets()
    {
        int max = Math.Max(1, patterns.Count);
        _entropyBuckets = new List<HashSet<Cell>>(max);
        for (int i = 0; i < max; i++) _entropyBuckets.Add(new HashSet<Cell>());
    }

    private void InitGrid(List<Pattern> possiblePatterns)
    {
        _positionToCell = new();
        _positionsToPossiblePatterns = new();
        _neighboursForPos = new();

        // first pass to init cells
        for (int y = 0; y < _dimensions.y; ++y)
        {
            for (int x = 0; x < _dimensions.x; ++x)
            {
                Vector2Int pos = new Vector2Int(x, y);
                var cell = new Cell(pos);
                _positionToCell[pos] = cell;

                _positionsToPossiblePatterns[pos] = new HashSet<Pattern>(possiblePatterns);

                _entropyBuckets[possiblePatterns.Count - 1].Add(cell);
            }
        }

        // second pass to init neighbours
        for (int y = 0; y < _dimensions.y; ++y)
        {
            for (int x = 0; x < _dimensions.x; ++x)
            {
                var pos = new Vector2Int(x, y);

                var northCellPos = pos + Direction.NORTH.ToVector();
                var southCellPos = pos + Direction.SOUTH.ToVector();
                var westCellPos = pos + Direction.WEST.ToVector();
                var eastCellPos = pos + Direction.EAST.ToVector();

                _positionToCell.TryGetValue(northCellPos, out var northCell);
                _positionToCell.TryGetValue(southCellPos, out var southCell);
                _positionToCell.TryGetValue(westCellPos, out var westCell);
                _positionToCell.TryGetValue(eastCellPos, out var eastCell);

                _neighboursForPos.Add(pos, new());
                _neighboursForPos[pos].Add(Direction.NORTH, northCell);
                _neighboursForPos[pos].Add(Direction.SOUTH, southCell);
                _neighboursForPos[pos].Add(Direction.WEST, westCell);
                _neighboursForPos[pos].Add(Direction.EAST, eastCell);
            }
        }
    }

    public override EntropyResult ReduceByNeighbors(Cell cell)
    {
        var possiblePatterns = _positionsToPossiblePatterns[cell.Pos];
        int before = possiblePatterns.Count;

        if (before == 0) return new EntropyResult(0, 0);

        foreach (var (dirToNeigh, neighbour) in _neighboursForPos[cell.Pos])
        {
            if (neighbour == null || possiblePatterns.Count == 0) continue;

            var neighDirToMe = dirToNeigh.GetOpposite();
            var neighbourPossibilities = _positionsToPossiblePatterns[neighbour.Pos];
            if (neighbour.Collapsed)
            {
                var pn = neighbourPossibilities.First();
                if (_patternToDirectionsToAllowedPatterns.TryGetValue(pn, out var dmap) &&
                    dmap.TryGetValue(neighDirToMe, out var allowed))
                {
                    possiblePatterns.IntersectWith(allowed);
                }
                else
                {
                    possiblePatterns.Clear();
                }
            }
            else
            {
                _unionSet.Clear();
                foreach (var np in neighbourPossibilities)
                    if (_patternToDirectionsToAllowedPatterns.TryGetValue(np, out var dmap) &&
                        dmap.TryGetValue(neighDirToMe, out var allowed))
                        _unionSet.UnionWith(allowed);

                if (_unionSet.Count == 0) possiblePatterns.Clear();
                else
                {
                    possiblePatterns.IntersectWith(_unionSet);
                }
            }
        }

        int after = possiblePatterns.Count;

        if (after != before)
        {
            _entropyBuckets[before - 1].Remove(cell);

            if (after >= 2) _entropyBuckets[after - 1].Add(cell);
        }

        return new EntropyResult(before, after);
    }

    private double ComputeEntropy(Cell cell)
    {
        const double EntropyNoise = 1e-9;

        if (cell.Collapsed)
            return 0.0;

        double W = 0.0;
        double Wlog = 0.0;

        var possibilitiesForCell = _positionsToPossiblePatterns[cell.Pos];

        foreach (var p in possibilitiesForCell)
        {
            double w = _weights[p];
            W += w;
            Wlog += _weightLogs[p];
        }

        if (W <= 0.0) return double.NegativeInfinity;

        // Shannon entropy H = ln(W) - (Wlog / W)
        double H = Math.Log(W) - (Wlog / W);

        H += EntropyNoise * _random.NextDouble();

        return H;
    }
}
