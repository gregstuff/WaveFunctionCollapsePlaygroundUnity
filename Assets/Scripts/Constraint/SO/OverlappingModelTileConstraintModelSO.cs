using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

[CreateAssetMenu(menuName = "ProcGen/WFC/Overlap Model")]
public class OverlappingModelTileModelSO : ConstraintModelSO
{
    [HideInInspector][SerializeField] public int N;
    [HideInInspector][SerializeField] public int patternSize;
    [HideInInspector][SerializeField] public List<PatternData> patterns;
    [HideInInspector][SerializeField] public List<PatternAdjacency> adjacencies;

    [Header("Performance / Quality")]
    [SerializeField] private int minPatternFrequency = 1;

    private int _height;
    private int _width;

    private Dictionary<Direction, Dictionary<int, HashSet<int>>> _directionToPatternIdToCompatiblePatterns;
    private Dictionary<Vector2Int, HashSet<int>> _cellPositionAvailablePatternIDs;
    private Dictionary<int, PatternData> _patternIDToPattern;
    private Dictionary<int, HashSet<Vector2Int>> _entropyToPositions;
    private Dictionary<Vector2Int, Cell> _positionsToCells;
    private System.Random _random;

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
        _cellPositionAvailablePatternIDs = new();
        _entropyToPositions = new();
        _positionsToCells = new();
        _directionToPatternIdToCompatiblePatterns = new();
        _patternIDToPattern = new();

        var allPatternIDs = new List<int>();

        for (int i = 0; i < patterns.Count; ++i)
        {
            var selectedPattern = patterns[i];
            allPatternIDs.Add(selectedPattern.patternId);
            _patternIDToPattern.Add(selectedPattern.patternId, selectedPattern);
        }

        // init each cell...
        for (int y = 0; y < _height; ++y)
        {
            for (int x = 0; x < _width; ++x)
            {
                var pos = new Vector2Int(x, y);
                _cellPositionAvailablePatternIDs[pos] = new HashSet<int>(allPatternIDs);
                _positionsToCells[pos] = new Cell(pos);
            }
        }

        // init entropy buckets
        for (int i = 1; i <= patterns.Count; ++i)
        {
            HashSet<Vector2Int> items = null;

            // initially, all cells start with all possibilities
            if (i == patterns.Count) items = new HashSet<Vector2Int>(_cellPositionAvailablePatternIDs.Keys.ToList());
            else items = new HashSet<Vector2Int>();

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
    public override CollapseUpdate CollapseCell(Cell cell)
    {
        var pos = cell.Pos;
        var availablePatternIDs = _cellPositionAvailablePatternIDs[pos];
        var patternCount = availablePatternIDs.Count;
        PatternData selectedPattern = null;

        double totalWeight = 0;

        foreach (var patternID in availablePatternIDs)
        {
            var pattern = _patternIDToPattern[patternID];
            totalWeight += pattern.weight;
        }


        double randomSelection = _random.NextDouble() * totalWeight;
        double curr = 0;

        foreach (var patternID in availablePatternIDs)
        {
            var pattern = _patternIDToPattern[patternID];
            curr += pattern.weight;
            if (curr >= randomSelection)
            {
                selectedPattern = pattern;
                break;
            }
        }

        // set selected pattern for this cell..
        _cellPositionAvailablePatternIDs[pos].Clear();
        _cellPositionAvailablePatternIDs[pos].Add(selectedPattern.patternId);

        // mark cell as collapsed...
        cell.Collapse();

        //we don't need a reference here anymore...
        _entropyToPositions[patternCount].Remove(pos);


        return new CollapseUpdate
        {
            Cell = cell.Pos,
            N = N,
            PatternId = selectedPattern.patternId,
            Tiles = selectedPattern.tilePattern
        };
    }

    public override void EnqueueNeighbours(Cell cell, Queue<Cell> candidates)
    {
        foreach (var dir in DirectionExtensions.Cardinal)
        {
            var vec = dir.ToGridVector();
            var neighbourPos = vec + cell.Pos;

            if (!_positionsToCells.TryGetValue(neighbourPos, out var neighbour)
                || neighbour.InQueue
                || neighbour.Collapsed) continue;

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
        void HandleCollapsedNeighbour(
            Direction myDirectionToNeighbour,
            Cell neighbour,
            HashSet<int> allowedPatternIDs)
        {
            var neighbourPatternID = _cellPositionAvailablePatternIDs[neighbour.Pos].First();
            var neighbourPattern = _patternIDToPattern[neighbourPatternID];

            // from the neighbour to the current cell, what patterns is the current cell allowed to have?
            var compattiblePatternsForCollapsed =
                _directionToPatternIdToCompatiblePatterns[myDirectionToNeighbour.GetOpposite()][neighbourPattern.patternId];

            allowedPatternIDs.IntersectWith(compattiblePatternsForCollapsed);
        }

        void HandleUncollapsedNeighbour(
            Direction myDirectionToNeighbour,
            Cell neighbour,
            HashSet<int> allowedPatternIDs)
        {
            var neighbourPatterns = _cellPositionAvailablePatternIDs[neighbour.Pos];
            var oppositeDir = myDirectionToNeighbour.GetOpposite();

            if (!_directionToPatternIdToCompatiblePatterns.TryGetValue(oppositeDir, out var dirCompatibilities))
            {
                allowedPatternIDs.Clear();
                return;
            }

            // Build set of ALL patterns that are compatible with ANY neighbor pattern
            var allCompatiblePatterns = new HashSet<int>();
            foreach (var neighbourPatternID in neighbourPatterns)
            {
                if (dirCompatibilities.TryGetValue(neighbourPatternID, out var compatibleSet))
                {
                    allCompatiblePatterns.UnionWith(compatibleSet);
                }
            }

            allowedPatternIDs.IntersectWith(allCompatiblePatterns);
        }

        var startingEntropy = _cellPositionAvailablePatternIDs[cell.Pos].Count;

        var currentPatterns = _cellPositionAvailablePatternIDs[cell.Pos];
        var allowedPatternIDs = new HashSet<int>(currentPatterns);

        foreach (var dir in DirectionExtensions.Cardinal)
        {
            var vec = dir.ToGridVector();
            var neighbourPos = vec + cell.Pos;

            if (!_positionsToCells.TryGetValue(neighbourPos, out var neighbour)) continue;

            if (neighbour.Collapsed) HandleCollapsedNeighbour(dir, neighbour, allowedPatternIDs);
            else HandleUncollapsedNeighbour(dir, neighbour, allowedPatternIDs);
        }

        _cellPositionAvailablePatternIDs[cell.Pos] = allowedPatternIDs;

        var finishingEntropy = _cellPositionAvailablePatternIDs[cell.Pos].Count;

        Debug.Log($"check for ${cell.Pos}");
        Debug.Log($"starting entropy: {startingEntropy}, finishing entropy: {finishingEntropy}");

        // update entropy buckets...

        _entropyToPositions[startingEntropy].Remove(cell.Pos);

        if (finishingEntropy > 0)
            _entropyToPositions[finishingEntropy].Add(cell.Pos);

        return new EntropyResult(startingEntropy, finishingEntropy);
    }
}