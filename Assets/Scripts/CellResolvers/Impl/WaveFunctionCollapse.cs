using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WaveFunctionCollapse : ITilemapResolver
{
    private ConstraintModelSO _constraintModel;
    private Action<Vector2Int, TileBase> _tileBaseChangedCallback;

    public override void ResolveTilemap(
        ConstraintModelSO constraintModel,
        Action<Vector2Int, TileBase> TileBaseChangedCallback)
    {
        #region Setup
        _constraintModel = constraintModel;
        _tileBaseChangedCallback = TileBaseChangedCallback;
        #endregion

        StartCoroutine("TilemapResolutionRoutine");
    }

    private IEnumerator TilemapResolutionRoutine()
    {
        Queue<Cell> candidates = new();
        while (true)
        {
            candidates.Clear();

            Cell target = _constraintModel.GetNext();

            if (target == null)
            {
                // no valid target - wfc has finished
                yield break;
            }

            CollapseCell(target);

            _constraintModel.EnqueueNeighbours(target, candidates);

            while (candidates.Count > 0)
            {
                var cand = candidates.Dequeue();

                var entropy = _constraintModel.ReduceByNeighbors(cand);

                // if the entropy has changed, then enqueue neighbours to see if their entropy will change as well
                // entropy oldEntropy propagate until entropy is 0
                if (entropy.HasDiff())
                {
                    _constraintModel.EnqueueNeighbours(cand, candidates);
                }

                if (entropy.NoEntropy())
                {
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
        var selectedTile = _constraintModel.CollapseCell(c.Pos);
        _tileBaseChangedCallback?.Invoke(c.Pos, selectedTile);
    }

}
