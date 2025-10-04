using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class WaveFunctionCollapse : TilemapResolver
{
    private ConstraintModelSO _constraintModel;
    private Action<CollapseUpdate> _tileBaseChangedCallback;

    static readonly ProfilerMarker WFC_Collapse = new ProfilerMarker("WFC.CollapseCell");
    static readonly ProfilerMarker WFC_Enqueue = new ProfilerMarker("WFC.EnqueueNeighbours");
    static readonly ProfilerMarker WFC_Reduce = new ProfilerMarker("WFC.ReduceByNeighbors");
    static readonly ProfilerMarker WFC_GetNext = new ProfilerMarker("WFC.GetNext");

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
        var start = DateTime.Now;
        Queue<Cell> candidates = new();
        while (true)
        {
            Cell target;
            candidates.Clear();

            using (WFC_GetNext.Auto())
            {
                target = _constraintModel.GetNext();
            }

            if (target == null)
            {
                // no valid target - wfc has finished
                Debug.Log($"Finished! It's done my friends it took {(DateTime.Now - start).Seconds} seconds");
                yield break;
            }

            using (WFC_Collapse.Auto())
            {
                CollapseCell(target);
            }

            using (WFC_Enqueue.Auto())
            {
                _constraintModel.EnqueueNeighbours(target, candidates);
            }

            while (candidates.Count > 0)
            {
                var cand = candidates.Dequeue();
                cand.InQueue = false;
                EntropyResult entropy;

                using (WFC_Reduce.Auto())
                {
                    entropy = _constraintModel.ReduceByNeighbors(cand);
                }

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
