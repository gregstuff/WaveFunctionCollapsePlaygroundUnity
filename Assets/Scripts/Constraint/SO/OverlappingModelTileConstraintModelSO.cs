using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Profiling;
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

    private Dictionary<int, HashSet<Vector2Int>> _entropyToPositions;
    private Dictionary<Vector2Int, Cell> _positionsToCells;
    private System.Random _random;

    private ModelDataMany _md;
    private GridIndex _gi;

    private ulong[] _tmpUnion;

    private Dictionary<int, int> _patternIDToIndex;
    private PatternData[] _indexToPattern;

    private static readonly byte[] _TZC_BYTE = BuildTzcByte();
    private static byte[] BuildTzcByte()
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            byte v = (byte)i, n = 0;
            if (v == 0) { lut[i] = 8; continue; }
            while ((v & 1) == 0) { v >>= 1; n++; }
            lut[i] = n;
        }
        return lut;
    }

    public OverlappingModelTileModelSO()
    {
        patterns = new List<PatternData>();
        adjacencies = new List<PatternAdjacency>();
    }

    private enum Dir { North = 0, East = 1, South = 2, West = 3 }

    private sealed class ModelDataMany
    {
        public int Width, Height, P, Blocks;
        public ulong TailMask;

        public ulong[][][] Compat;

        public ulong[] AllowedPerCellFlat;
        public PatternData[] PatternsByIndex;
    }

    private sealed class GridIndex
    {
        private readonly int _w, _h;
        public readonly int[] Offset = new int[4];

        public GridIndex(int width, int height)
        {
            _w = width; _h = height;
            Offset[(int)Dir.North] = _w;
            Offset[(int)Dir.East] = 1;
            Offset[(int)Dir.South] = -_w;
            Offset[(int)Dir.West] = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Idx(int x, int y) => y * _w + x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNeighbour(int idx, Dir d, out int nIdx)
        {
            int x = idx % _w;
            int y = idx / _w;
            switch (d)
            {
                case Dir.North:
                    y += 1;
                    break;
                case Dir.East:
                    x += 1;
                    break;
                case Dir.South:
                    y -= 1;
                    break;
                case Dir.West:
                    x -= 1;
                    break;
            }

            // 2 comparisons rather than 4
            if ((uint)x < (uint)_w && (uint)y < (uint)_h)
            {
                nIdx = Idx(x, y);
                return true;
            }
            nIdx = -1;
            return false;
        }
    }

    private static class BitChunks
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlocksFor(int P) => (P + 63) >> 6;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong TailMaskFor(int P)
        {
            int r = P & 63;
            return r == 0 ? ulong.MaxValue : ((1UL) << r) - 1UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ulong[] blocks, int bitId)
        {
            int b = bitId >> 6;
            int o = bitId & 63;
            blocks[b] |= 1UL << o;
        }
    }

    private class CompatBuilderMany
    {
        public static ulong[][][] BuildCompatChunked(
            int P,
            IEnumerable<PatternAdjacency> adjacencies,
            Func<int, int> denseIdMap,
            Func<Direction, Dir> mapDir,
            int dirCount = 4)
        {

            int blocks = BitChunks.BlocksFor(P);
            var compat = new ulong[dirCount][][];

            for (int d = 0; d < dirCount; ++d)
            {
                compat[d] = new ulong[P][];
                for (int p = 0; p < P; ++p)
                    compat[d][p] = new ulong[blocks];
            }

            foreach (var a in adjacencies)
            {
                int src = denseIdMap(a.sourcePatternId);
                if (src < 0) continue;

                int d = (int)mapDir(a.direction);
                var row = compat[d][src];

                foreach (var cid in a.compatiblePatternIds)
                {
                    int c = denseIdMap(cid);
                    if (c >= 0) BitChunks.SetBit(row, c);
                }
            }
            return compat;
        }
    }

    public override void Init(Vector2Int dimensions)
    {
        _random = new System.Random();
        _height = dimensions.y;
        _width = dimensions.x;
        _entropyToPositions = new();
        _positionsToCells = new();

        foreach (var p in patterns)
        {
            if (p.weight < minPatternFrequency) Debug.Log($"{p.patternId} was filtered out");
        }

        var filtered = patterns.Where(p => p.weight > minPatternFrequency).ToList();

        int P = patterns.Count;

        if (P == 0) throw new InvalidOperationException("No patterns available");

        _patternIDToIndex = new();
        _indexToPattern = new PatternData[P];

        for (int i = 0; i < P; ++i)
        {
            _patternIDToIndex[patterns[i].patternId] = i;
            _indexToPattern[i] = patterns[i];
        }

        Dir MapDir(Direction d) => d switch
        {
            Direction.NORTH => Dir.North,
            Direction.SOUTH => Dir.South,
            Direction.WEST => Dir.West,
            Direction.EAST => Dir.East,
            _ => throw new ArgumentOutOfRangeException(nameof(d)),
        };

        int blocks = BitChunks.BlocksFor(P);
        ulong tailMask = BitChunks.TailMaskFor(P);
        var compat = CompatBuilderMany.BuildCompatChunked(
            P,
            adjacencies,
            id => _patternIDToIndex.TryGetValue(id, out var d) ? d : -1,
            MapDir);

        int numCells = _width * _height;
        var allowedFlat = new ulong[numCells * blocks];
        for (int cell = 0; cell < numCells; ++cell)
        {
            int baseIdx = cell * blocks;
            for (int b = 0; b < blocks - 1; ++b)
                allowedFlat[baseIdx + b] = ulong.MaxValue;
            allowedFlat[baseIdx + (blocks - 1)] = tailMask;
        }

        _md = new ModelDataMany
        {
            Width = _width,
            Height = _height,
            P = P,
            Blocks = blocks,
            TailMask = tailMask,
            Compat = compat,
            AllowedPerCellFlat = allowedFlat,
            PatternsByIndex = _indexToPattern
        };

        _gi = new GridIndex(_width, _height);
        _tmpUnion = new ulong[_md.Blocks];

        // init each cell...
        for (int y = 0; y < _height; ++y)
        {
            for (int x = 0; x < _width; ++x)
            {
                var pos = new Vector2Int(x, y);
                _positionsToCells[pos] = new Cell(pos);
            }
        }

        // init entropy buckets
        _entropyToPositions.Clear();
        for (int i = 1; i <= _md.P; ++i)
        {
            _entropyToPositions[i] = (i == _md.P)
                ? new HashSet<Vector2Int>(_positionsToCells.Keys)
                : new HashSet<Vector2Int>();
        }

    }

    /*
     * 
     * Resolve selected pattern based on available patterns and weights for cell
     * 
     */
    public override CollapseUpdate CollapseCell(Cell cell)
    {
        int x = cell.Pos.x, y = cell.Pos.y;
        int idx = _gi.Idx(x, y);
        int baseIdx = idx * _md.Blocks;

        double totalWeight = 0;
        for (int b = 0; b < _md.Blocks; ++b)
        {
            ulong v = _md.AllowedPerCellFlat[baseIdx + b];
            if (b == _md.Blocks - 1) v &= _md.TailMask;

            while (v != 0)
            {
                int bit = FastBits.TrailingZeroCount(v);
                int p = (b << 6) + bit;
                if ((uint)p >= (uint)_md.P) break;

                totalWeight += _md.PatternsByIndex[p].weight;
                v &= v - 1;
            }
        }

        if (totalWeight <= 0)
            throw new InvalidOperationException("No available patterns");

        double r = _random.NextDouble() * totalWeight;
        PatternData selected = null;

        double acc = 0;
        for (int b = 0; b < _md.Blocks && selected == null; ++b)
        {
            ulong v = _md.AllowedPerCellFlat[baseIdx + b];
            if (b == _md.Blocks - 1) v &= _md.TailMask;   // <<< mask last block
            while (v != 0)
            {
                int bit = FastBits.TrailingZeroCount(v);
                int p = (b << 6) + bit;
                if ((uint)p >= (uint)_md.P) break;        // <<< defensive

                var pd = _md.PatternsByIndex[p];
                acc += pd.weight;
                if (acc >= r)
                {
                    selected = pd;
                    break;
                }
                v &= v - 1;
            }
        }

        if (selected == null)
            throw new InvalidOperationException("CollapseCell failed to select a pattern (tail-mask issue?)");

        for (int b = 0; b < _md.Blocks; ++b) _md.AllowedPerCellFlat[baseIdx + b] = 0UL;
        int selIndex = _patternIDToIndex[selected.patternId];
        int sb = selIndex >> 6;
        int so = selIndex & 63;
        _md.AllowedPerCellFlat[baseIdx + sb] = (1UL << so);

        cell.Collapse();

        for (int i = 1; i <= _md.P; ++i)
            _entropyToPositions[i].Remove(cell.Pos);
        _entropyToPositions[1].Add(cell.Pos);

        return new CollapseUpdate
        {
            Cell = cell.Pos,
            N = N,
            PatternId = selected.patternId,
            Tiles = selected.tilePattern
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
        for (int i = 2; i <= _md.P; ++i)
        {
            var set = _entropyToPositions[i];
            if (set.Count == 0) continue;

            int skip = _random.Next(set.Count);
            foreach (var pos in set)
            {
                if (skip-- == 0)
                {
                    var c = _positionsToCells[pos];
                    if (!c.Collapsed) return c;
                }
            }
        }
        return null;
    }


    public override EntropyResult ReduceByNeighbors(Cell cell)
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int BaseOf(int cellIdx) => cellIdx * _md.Blocks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int PopCountBlocks(ulong[] a, int @base, int blocks)
        {
            int sum = 0;
            for (int b = 0; b < blocks; ++b) sum += FastBits.PopCount(a[@base + b]);
            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TrySingleton(ulong[] a, int @base, int blocks, out int patternId)
        {
            int total = 0;
            int lastBlock = -1;
            ulong lastVal = 0;

            for (int b = 0; b < blocks; ++b)
            {
                ulong v = a[@base + b];
                if (v == 0) continue;
                int pc = FastBits.PopCount(v);
                total += pc;
                if (total > 1) { patternId = -1; return false; }
                lastBlock = b;
                lastVal = v;
            }

            if (total == 1)
            {
                patternId = (lastBlock << 6) + FastBits.TrailingZeroCount(lastVal);
                return true;
            }

            patternId = -1;
            return false;
        }

        int x = cell.Pos.x, y = cell.Pos.y;
        int idx = _gi.Idx(x, y);
        int aBase = BaseOf(idx);
        int startingEntropy = 0;


        startingEntropy = PopCountBlocks(_md.AllowedPerCellFlat, aBase, _md.Blocks);

        for (int d = 0; d < 4; ++d)
        {
            if (!_gi.TryNeighbour(idx, (Dir)d, out int nIdx)) continue;

            int od = d ^ 2;
            int nBase = BaseOf(nIdx);

            bool neighbourZero = true;
            for (int b = 0; b < _md.Blocks; ++b)
                if (_md.AllowedPerCellFlat[nBase + b] != 0) { neighbourZero = false; break; }

            if (neighbourZero)
            {
                for (int b = 0; b < _md.Blocks; ++b) _md.AllowedPerCellFlat[aBase + b] = 0UL;
                goto Finish;
            }

            if (TrySingleton(_md.AllowedPerCellFlat, nBase, _md.Blocks, out int nPat))
            {
                var row = _md.Compat[od][nPat];
                for (int b = 0; b < _md.Blocks; ++b)
                    _md.AllowedPerCellFlat[aBase + b] &= row[b];

                _md.AllowedPerCellFlat[aBase + (_md.Blocks - 1)] &= _md.TailMask;

                bool zeroNow = true;

                for (int b = 0; b < _md.Blocks; ++b)
                    if (_md.AllowedPerCellFlat[aBase + b] != 0) { zeroNow = false; break; }
                if (zeroNow) goto Finish;

                continue;
            }

            for (int b = 0; b < _md.Blocks; ++b) _tmpUnion[b] = 0UL;

            for (int b = 0; b < _md.Blocks; ++b)
            {
                ulong word = _md.AllowedPerCellFlat[nBase + b];
                if (b == _md.Blocks - 1) word &= _md.TailMask;

                for (int by = 0; by < 8; ++by)
                {
                    byte chunk = (byte)(word >> (by * 8));
                    if (chunk == 0) continue;

                    while (chunk != 0)
                    {
                        int bitInByte = _TZC_BYTE[chunk];
                        int p = (b << 6) + (by * 8 + bitInByte);
                        if ((uint)p < (uint)_md.P)
                        {
                            var row = _md.Compat[od][p];
                            for (int ub = 0; ub < _md.Blocks; ++ub)
                                _tmpUnion[ub] |= row[ub];
                        }
                        chunk = (byte)(chunk & (chunk - 1));

                        bool unionCoversAllowed = true;
                        for (int ub = 0; ub < _md.Blocks; ++ub)
                        {
                            ulong allowedBlock = _md.AllowedPerCellFlat[aBase + ub];
                            if ((allowedBlock & ~_tmpUnion[ub]) != 0UL) { unionCoversAllowed = false; break; }
                        }
                        if (unionCoversAllowed) goto ApplyAndMask;
                    }
                }
            }


        ApplyAndMask:
            for (int b = 0; b < _md.Blocks; ++b)
                _md.AllowedPerCellFlat[aBase + b] &= _tmpUnion[b];

            _md.AllowedPerCellFlat[aBase + (_md.Blocks - 1)] &= _md.TailMask;

            bool becameZero = true;
            for (int b = 0; b < _md.Blocks; ++b)
                if (_md.AllowedPerCellFlat[aBase + b] != 0) { becameZero = false; break; }
            if (becameZero) goto Finish;
        }

    Finish:
        int finishingEntropy = PopCountBlocks(_md.AllowedPerCellFlat, aBase, _md.Blocks);

        if (startingEntropy > 0)
            _entropyToPositions[startingEntropy].Remove(cell.Pos);
        if (finishingEntropy > 0)
            _entropyToPositions[finishingEntropy].Add(cell.Pos);

        return new EntropyResult(startingEntropy, finishingEntropy);

    }

}