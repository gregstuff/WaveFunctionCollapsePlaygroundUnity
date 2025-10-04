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
        Queue<Cell> candidates = new();

        const float maxMsPerFrame = 2.5f;  // tune this
        const int opsChunk = 64;   // yield every N ops to reduce hitching

        while (true)
        {
            var frameDeadline = Time.realtimeSinceStartup + maxMsPerFrame * 0.001f;

            // pick next and collapse (unchanged)
            var target = _constraintModel.GetNext();
            if (target == null) yield break;
            CollapseCell(target);
            _constraintModel.EnqueueNeighbours(target, candidates);

            int ops = 0;
            while (candidates.Count > 0)
            {
                var cand = candidates.Dequeue();
                cand.InQueue = false;

                var entropy = _constraintModel.ReduceByNeighbors(cand);
                if (entropy.NoEntropy()) { HandleContradiction("WFC contradiction.", cand); continue; }

                if (entropy.NewEntropy == 1 && !cand.Collapsed)
                {
                    CollapseCell(cand);
                    _constraintModel.EnqueueNeighbours(cand, candidates);
                }
                else if (entropy.HasDiff())
                {
                    _constraintModel.EnqueueNeighbours(cand, candidates);
                }

                // yield periodically to bound hitches
                if ((++ops % opsChunk == 0) && Time.realtimeSinceStartup >= frameDeadline)
                {
                    yield return null;
                    frameDeadline = Time.realtimeSinceStartup + maxMsPerFrame * 0.001f;
                }
            }

            // always yield once per outer step
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
