using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WaveFunctionCollapse : TilemapResolver
{
    private ConstraintModelSO _constraintModel;
    private Action<CollapseUpdate> _tileBaseChangedCallback;

    public override void ResolveTilemap(
        ConstraintModelSO constraintModel,
        Action<CollapseUpdate> TileBaseChangedCallback)
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
                cand.InQueue = false;

                var entropy = _constraintModel.ReduceByNeighbors(cand);


                //Debug.Log($"coinsider {cand.Pos}");

                if (entropy.NoEntropy())
                {
                    HandleContradiction($"WFC contradiction.", cand);
                    continue;
                }

                if (entropy.NewEntropy == 1 && !cand.Collapsed)
                {
                    CollapseCell(cand);
                    _constraintModel.EnqueueNeighbours(cand, candidates);
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // if the entropy has changed, then enqueue neighbours to see if their entropy will change as well
                // entropy oldEntropy propagate until entropy is 0
                if (entropy.HasDiff())
                {
                    _constraintModel.EnqueueNeighbours(cand, candidates);
                }

                //yield return null;
            }

            yield return null;
        }
    }

    private void HandleContradiction(string contradiction, Cell c)
    {
        if (_constraintModel.IgnoreContradictions)
        {
            c.Collapsed = true;
            return;
        }

        Debug.LogError(contradiction);

        throw new UnfinishableMapException();
    }

    private void CollapseCell(Cell c)
    {
        var collapseUpdate = _constraintModel.CollapseCell(c);
        _tileBaseChangedCallback?.Invoke(collapseUpdate);

    }

}
