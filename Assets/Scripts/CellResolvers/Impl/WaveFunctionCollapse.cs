using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        const float maxMsPerFrame = 2.5f;
        const int opsChunk = 64;

        while (true)
        {
            var frameDeadline = Time.realtimeSinceStartup + maxMsPerFrame * 0.001f;

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

                // yield only after we have done substantial work AND enough time has passed
                if ((++ops % opsChunk == 0) && Time.realtimeSinceStartup >= frameDeadline)
                {
                    yield return null;
                    frameDeadline = Time.realtimeSinceStartup + maxMsPerFrame * 0.001f;
                }
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
