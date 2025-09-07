using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WaveFunctionCollapse : ITilemapResolver
{

    [SerializeField] private float cellResolutionDelay = 0.5f;

    private Dictionary<Vector2Int, Dictionary<Direction, Cell>> neighboursForPos;
    private ConstraintModelSO _constraintModel;
    private Dictionary<Vector2Int, Cell> _cells;
    private Action<Cell> _tileBaseChangedCallback;

    private List<HashSet<Cell>> _buckets;
    private int _maxEntropy;

    public override void ResolveTilemap(
        ConstraintModelSO constraintModel,
        Dictionary<Vector2Int, Cell> cells,
        Action<Cell> TileBaseChangedCallback)
    {
        #region Setup
        _constraintModel = constraintModel;
        _tileBaseChangedCallback = TileBaseChangedCallback;
        _cells = cells;
        Init();
        InitBuckets(out _maxEntropy);
        #endregion

        StartCoroutine("TilemapResolutionRoutine");
    }

    private void Init()
    {
        neighboursForPos = new();


        foreach (var cellKVP in _cells)
        {
            var cell = cellKVP.Value;
            var pos = cellKVP.Key;

            if (cell.Collapsed) continue;

            var northCellPos = pos + Direction.NORTH.ToVector();
            var southCellPos = pos + Direction.SOUTH.ToVector();
            var westCellPos = pos + Direction.WEST.ToVector();
            var eastCellPos = pos + Direction.EAST.ToVector();

            _cells.TryGetValue(northCellPos, out var northCell);
            _cells.TryGetValue(southCellPos, out var southCell);
            _cells.TryGetValue(westCellPos, out var westCell);
            _cells.TryGetValue(eastCellPos, out var eastCell);

            neighboursForPos.Add(pos, new());
            neighboursForPos[pos].Add(Direction.NORTH, northCell);
            neighboursForPos[pos].Add(Direction.SOUTH, southCell);
            neighboursForPos[pos].Add(Direction.WEST, westCell);
            neighboursForPos[pos].Add(Direction.EAST, eastCell);
        }
    }

    private void InitBuckets(out int maxEntropy)
    {
        maxEntropy = _cells.Values.Max(c => c.PossibleTiles.Count);
        _buckets = new List<HashSet<Cell>>(maxEntropy + 1);
        for (int i = 0; i <= maxEntropy; i++) _buckets.Add(new HashSet<Cell>());

        foreach (var c in _cells.Values)
        {
            if (c.Collapsed) continue;
            var e = c.PossibleTiles.Count;
            e = Mathf.Clamp(e, 0, maxEntropy);
            _buckets[e].Add(c);
        }
    }

    private IEnumerator TilemapResolutionRoutine()
    {
        Queue<Cell> candidates = new();
        while (true)
        {
            Cell target = null;
            int entropy = 0;
            candidates.Clear();

            for (int e = 1; e < _buckets.Count; e++)
            {
                if (_buckets[e].Count > 0)
                {
                    var randIndex = UnityEngine.Random.Range(0, _buckets[e].Count);
                    entropy = e;
                    target = _buckets[e].ElementAt(randIndex);
                    break;
                }
            }

            if (target == null)
            {
                // no valid target - may just have finished
                yield break;
            }

            _buckets[entropy].Remove(target);

            CollapseCell(target);

            EnqueueNeighbors(target, candidates);

            while (candidates.Count > 0)
            {
                var cand = candidates.Dequeue();

                var (oldEntropy, newEntropy) = ReduceByNeighbors(cand);

                // if the entropy has changed, then enqueue neighbours to see if their entropy will change as well
                // entropy oldEntropy propagate until entropy is 0
                if (newEntropy != oldEntropy)
                {
                    _buckets[oldEntropy].Remove(cand);
                    _buckets[newEntropy].Add(cand);
                    EnqueueNeighbors(cand, candidates);
                }

                if (newEntropy == 0)
                {
                    _buckets[0].Remove(cand);
                    HandleContradiction($"WFC contradiction at {cand.Pos}.");
                }

            }

            yield return null;
        }
    }

    private void HandleContradiction(string contradiction)
    {
        if (_constraintModel.IgnoreContradictions) return;

        Debug.LogError(contradiction);

        throw new UnfinishableMapException();
    }

    private void CollapseCell(Cell c)
    {
        int idx = UnityEngine.Random.Range(0, c.PossibleTiles.Count);
        var chosen = c.PossibleTiles.ElementAt(idx);
        c.Collapse(chosen);

        _tileBaseChangedCallback?.Invoke(c);
    }

    private void EnqueueNeighbors(Cell c, Queue<Cell> candidates)
    {
        var neighboursMap = neighboursForPos[c.Pos];
        foreach (var kvp in neighboursMap)
        {
            var neighbour = kvp.Value;
            if (neighbour != null && !neighbour.Collapsed && !candidates.Contains(neighbour)) candidates.Enqueue(neighbour);
        }
    }

    private (int, int) ReduceByNeighbors(Cell cell)
    {
        void HandleCollapsedNeighbour(Direction neighborDirToMe, Cell neighbour)
        {
            var neighborDict = _constraintModel.GetDirectionTilesForTile(neighbour.Tile);
            var allowed = neighborDict[neighborDirToMe];

            cell.PossibleTiles.RemoveWhere(t => !allowed.Contains(t));
        }

        void HandleUncollapsedNeighbour(Direction myDirectionToNeighbour, Cell neighbour)
        {
            TileBase[] toRemove = new TileBase[cell.PossibleTiles.Count];
            int ptr = 0;

            // look at potential neighbour states
            // for each of my potential states, can my potential state exist for the given potential states of my neighbour?
            foreach (var possibleTile in cell.PossibleTiles)
            {
                var potentialMatches = _constraintModel.GetDirectionTilesForTile(possibleTile)[myDirectionToNeighbour];
                var canHappen = potentialMatches.Any(potentialMatch => neighbour.PossibleTiles.Contains(potentialMatch));
                if (!canHappen) toRemove[ptr++] = possibleTile;
            }

            // if we found any tiles that aren't possible anymore, get rid of them
            for (int i = 0; i < toRemove.Length; ++i)
            {
                if (toRemove[i] == null) break;
                cell.PossibleTiles.Remove(toRemove[i]);
            }

        }

        int before = cell.PossibleTiles.Count;

        foreach (var (dirToNeighbour, neighbour) in neighboursForPos[cell.Pos])
        {
            if (neighbour == null) continue;

            var myDirToNeighbor = dirToNeighbour;
            var neighborDirToMe = dirToNeighbour.GetOpposite();

            if (neighbour.Collapsed) HandleCollapsedNeighbour(neighborDirToMe, neighbour);
            else HandleUncollapsedNeighbour(myDirToNeighbor, neighbour);
        }

        int after = cell.PossibleTiles.Count;

        return (before, after);
    }

}
