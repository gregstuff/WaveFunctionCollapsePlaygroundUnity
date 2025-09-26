using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "ProcGen/WFC/Overlap Model")]
public class OverlappingModelTileModelSO : ConstraintModelSO
{
    [Header("Model Data (input)")]
    [SerializeField] public int N;
    [SerializeField] public int patternSize;
    [SerializeField] public List<PatternData> patterns;
    [SerializeField] public List<PatternAdjacency> adjacencies;

    [Header("Performance / Quality")]
    [SerializeField] private int minPatternFrequency = 1;

    private int _height;
    private int _width;

    private Dictionary<Direction, Dictionary<int, HashSet<int>>> _directionToPatternIdToCompatiblePatterns;
    private Dictionary<Vector2Int, List<PatternData>> _cellPositionAvailablePatterns;
    private Dictionary<int, HashSet<Vector2Int>> _entropyToPositions;
    private Dictionary<Vector2Int, Cell> _positionsToCells;
    private System.Random _random;

    [Serializable]
    public class PatternData
    {
        public int patternId;
        public TileBase[] tilePattern;
        public double weight;
    }

    [Serializable]
    public class PatternAdjacency
    {
        public int sourcePatternId;
        public Direction direction;
        public List<int> compatiblePatternIds = new List<int>();
    }

    public OverlappingModelTileModelSO()
    {
        patterns = new List<PatternData>();
        adjacencies = new List<PatternAdjacency>();
    }

    public override void Init(Vector2Int dimensions)
    {
        _random = new System.Random();
        _height = dimensions.y;
        _width = dimensions.x;
        _cellPositionAvailablePatterns = new();
        _entropyToPositions = new();
        _positionsToCells = new();
        _directionToPatternIdToCompatiblePatterns = new();

        // init each cell...
        for (int y = 0; y < _height; ++y)
        {
            for (int x = 0; x < _width; ++x)
            {
                var pos = new Vector2Int(x, y);
                _cellPositionAvailablePatterns[pos] = new List<PatternData>(patterns);
                _positionsToCells[pos] = new Cell(pos);
            }
        }

        // init entropy buckets
        for (int i = 1; i <= patterns.Count; ++i)
        {
            HashSet<Vector2Int> items = null;

            // initially, all cells start with all possibilities
            if (i == patterns.Count) items = new HashSet<Vector2Int>(_cellPositionAvailablePatterns.Keys.ToList());
            else _entropyToPositions[i] = new HashSet<Vector2Int>();

            _entropyToPositions[i] = items;
        }

        // what patterns are allowed to be next to each other for a given direction?
        foreach (var adjacency in adjacencies)
        {
            if (!_directionToPatternIdToCompatiblePatterns.TryGetValue(adjacency.direction, out var patternToCompatiblePatterns))
            {
                _directionToPatternIdToCompatiblePatterns[adjacency.direction] = new Dictionary<int, HashSet<int>>();
                patternToCompatiblePatterns = _directionToPatternIdToCompatiblePatterns[adjacency.direction];
            }

            if (!patternToCompatiblePatterns.TryGetValue(adjacency.sourcePatternId, out var compatiblePatterns))
            {
                _directionToPatternIdToCompatiblePatterns[adjacency.direction][adjacency.sourcePatternId] = new HashSet<int>();
                compatiblePatterns = _directionToPatternIdToCompatiblePatterns[adjacency.direction][adjacency.sourcePatternId];
            }

            adjacency.compatiblePatternIds.ForEach(id => compatiblePatterns.Add(id));
        }

    }

    /*
     * 
     * Resolve selected pattern based on available patterns and weights for cell
     * 
     */
    public override TileBase CollapseCell(Cell cell)
    {
        var pos = cell.Pos;
        var availablePatterns = _cellPositionAvailablePatterns[pos];
        var patternCount = availablePatterns.Count;
        PatternData selectedPattern = null;

        double totalWeight = 0;

        for (int i = 0; i < availablePatterns.Count; ++i)
            totalWeight += availablePatterns[i].weight;

        double randomSelection = _random.NextDouble() * totalWeight;
        double curr = 0;

        for (int i = 0; i < patternCount; ++i)
        {
            curr += availablePatterns[i].weight;
            if (curr >= randomSelection) selectedPattern = availablePatterns[i];
        }

        // set selected pattern for this cell..
        _cellPositionAvailablePatterns[pos].Clear();
        _cellPositionAvailablePatterns[pos].Add(selectedPattern);

        // mark cell as collapsed...
        cell.Collapse();

        //we don't need a reference here anymore...
        _entropyToPositions[patternCount].Remove(pos);

        //set this cell to the top left corner
        return selectedPattern.tilePattern[0];
    }

    public override void EnqueueNeighbours(Cell cell, Queue<Cell> candidates)
    {
        foreach (var dir in DirectionExtensions.Cardinal)
        {
            var vec = dir.ToVector();
            var neighbourPos = vec + cell.Pos;
            var periodicPos = new Vector2Int(neighbourPos.x % _width, neighbourPos.y % _height);

            var neighbour = _positionsToCells[periodicPos];

            if (neighbour.InQueue || neighbour.Collapsed) continue;

            candidates.Enqueue(neighbour);
            neighbour.InQueue = true;
        }
    }

    public override Cell GetNext()
    {

        Vector2Int pos;

        for (int i = 1; i <= patterns.Count; ++i)
        {
            if (_entropyToPositions[i].Count > 0)
            {
                pos = _entropyToPositions[i].First();
                return _positionsToCells[pos];
            }
        }

        // we're finished...
        return null;
    }

    public override EntropyResult ReduceByNeighbors(Cell cell)
    {
        void HandleCollapsedNeighbour(Direction myDirectionToNeighbour, Cell neighbour)
        {
            // we know there's only one pattern...
            var neighbourPattern = _cellPositionAvailablePatterns[neighbour.Pos][0];
            var cellPatterns = _cellPositionAvailablePatterns[cell.Pos];

            // from the neighbour to the current cell, what patterns is the current cell allowed to have?
            var compattiblePatternsForCollapsed =
                _directionToPatternIdToCompatiblePatterns[myDirectionToNeighbour.GetOpposite()][neighbourPattern.patternId];

            var allowedPatterns = cellPatterns.Where(p => compattiblePatternsForCollapsed.Contains(p.patternId));

            _cellPositionAvailablePatterns[cell.Pos] = allowedPatterns.ToList();
        }

        void HandleUncollapsedNeighbour(Direction myDirectionToNeighbour, Cell neighbour)
        {
            var neighbourPatterns = _cellPositionAvailablePatterns[neighbour.Pos];
            var cellPatterns = _cellPositionAvailablePatterns[cell.Pos];

            List<PatternData> stillAllowedPatterns = new List<PatternData>();

            foreach (var cellPattern in cellPatterns)
            {
                if (_directionToPatternIdToCompatiblePatterns.TryGetValue(myDirectionToNeighbour, out var patternCompatibilities) &&
                    patternCompatibilities.TryGetValue(cellPattern.patternId, out var compatiblePatternIds))
                {
                    bool isCompatibleWithAnyNeighborPattern = neighbourPatterns
                        .Any(neighborPattern => compatiblePatternIds.Contains(neighborPattern.patternId));

                    if (isCompatibleWithAnyNeighborPattern)
                    {
                        stillAllowedPatterns.Add(cellPattern);
                    }
                }
            }

            _cellPositionAvailablePatterns[cell.Pos] = stillAllowedPatterns;
        }

        var startingEntropy = _cellPositionAvailablePatterns[cell.Pos].Count;

        foreach (var dir in DirectionExtensions.Cardinal)
        {
            var vec = dir.ToVector();
            var neighbourPos = vec + cell.Pos;
            var periodicPos = new Vector2Int(neighbourPos.x % _width, neighbourPos.y % _height);

            var neighbour = _positionsToCells[periodicPos];

            if (neighbour.Collapsed) HandleCollapsedNeighbour(dir, neighbour);
            else HandleUncollapsedNeighbour(dir, neighbour);
        }

        var finishingEntropy = _cellPositionAvailablePatterns[cell.Pos].Count;

        // update entropy buckets...
        _entropyToPositions[startingEntropy].Remove(cell.Pos);
        _entropyToPositions[finishingEntropy].Add(cell.Pos);

        return null;
    }
}