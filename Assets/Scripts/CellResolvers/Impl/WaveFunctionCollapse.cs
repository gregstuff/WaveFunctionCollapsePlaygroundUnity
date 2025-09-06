using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        List<Cell> candidates = new();
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
                Debug.LogError("WFC contradiction: no valid target!");
                yield break;
            }

            _buckets[entropy].Remove(target);

            CollapseCell(target);

            EnqueueNeighbors(target, candidates);

            foreach (var cand in candidates)
            {
                var oldE = Mathf.Clamp(cand.PossibleTiles.Count, 0, _maxEntropy);
                ReduceByNeighbors(cand);
                var newE = Mathf.Clamp(cand.PossibleTiles.Count, 0, _maxEntropy);

                if (newE != oldE)
                {
                    _buckets[oldE].Remove(cand);
                    _buckets[newE].Add(cand);
                }

                if (newE == 0)
                {
                    Debug.LogError($"WFC contradiction at {cand.Pos}.");
                    _buckets[0].Remove(cand);
                    //yield break;
                }

            }

            yield return new WaitForSeconds(cellResolutionDelay);
        }
    }

    private void CollapseCell(Cell c)
    {
        Debug.Log($"collapsing {c.Pos}");
        int idx = UnityEngine.Random.Range(0, c.PossibleTiles.Count);
        var chosen = c.PossibleTiles.ElementAt(idx);
        c.Collapse(chosen);

        _tileBaseChangedCallback?.Invoke(c);
    }

    private void EnqueueNeighbors(Cell c, List<Cell> candidates)
    {
        var neighboursMap = neighboursForPos[c.Pos];
        Debug.Log($"Candidates count before: {candidates.Count}");
        foreach (var kvp in neighboursMap)
        {
            var nb = kvp.Value;
            if (nb != null && !nb.Collapsed) candidates.Add(nb);
        }
        Debug.Log($"Candidates count after: {candidates.Count}");
    }

    private bool ReduceByNeighbors(Cell cell)
    {
        bool changed = false;

        int before = cell.PossibleTiles.Count;

        foreach (var (dirToNb, nb) in neighboursForPos[cell.Pos])
        {
            if (nb == null || !nb.Collapsed) continue;

            var myDirToNeighbor = dirToNb;
            var neighborDirToMe = dirToNb.GetOpposite();

            var neighborDict = _constraintModel.GetDirectionTilesForTile(nb.Tile);
            var allowed = neighborDict[neighborDirToMe];

            cell.PossibleTiles.RemoveWhere(t => !allowed.Contains(t));
        }

        if (cell.PossibleTiles.Count != before) changed = true;
        return changed;
    }

}
