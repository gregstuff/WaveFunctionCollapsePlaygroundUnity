using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "ProcGen/WFC/Overlap Model")]
public class OverlappingModelTileModelSO : ConstraintModelSO
{
    [Header("Model Data (input)")]
    [SerializeField] public int N;
    [SerializeField] public List<PatternFrequency> patterns;
    [SerializeField] public List<PatternAdjacency> compatibilities;

    [Header("Performance / Quality")]
    [SerializeField] private int minPatternFrequency = 1;

    [Serializable] public class PatternFrequency { public Pattern pattern; public int count; }
    [Serializable] public class DirectionAdjacency { public Direction direction; public Pattern target; public int count; }
    [Serializable] public class PatternAdjacency { public Pattern source; public List<DirectionAdjacency> edges = new(); }

    private Vector2Int _dimensions;
    private int _width, _height, _cellCount;

    private Dictionary<Pattern, int> _patternToID;
    private Pattern[] _patternsByID;
    private int[] _weightsByPatternID;
    private double[] _weightLogsByPatternID;
    private int _P;
    private int _wordsPerMask;

    private ulong[][][] _allowedMasks;

    private ulong[] _poss;
    private int[] _count;
    private int[] _collapsedId;
    private Cell[] _cells;
    private bool[] _inQueue;

    private int[] _neighN, _neighS, _neighW, _neighE;

    private Queue<int>[] _byCardinality;
    private System.Random _random = new System.Random();

    public OverlappingModelTileModelSO()
    {
        patterns = new();
        compatibilities = new();
    }

    private struct UnionKey : IEquatable<UnionKey>
    {
        public byte dir;
        public ulong hash;
        public bool Equals(UnionKey other) => dir == other.dir && hash == other.hash;
        public override int GetHashCode() => ((dir << 3) * 73856093) ^ (int)hash ^ (int)(hash >> 32);
    }
    private Dictionary<UnionKey, ulong[]> _unionCache;

    private const int DIR_N = 0, DIR_S = 1, DIR_W = 2, DIR_E = 3;

    public override void Init(Vector2Int dimensions)
    {
        _dimensions = dimensions;
        _width = dimensions.x;
        _height = dimensions.y;
        _cellCount = _width * _height;

        _staticWidth = _width;

        BuildPatternsAndWeights();
        BuildAllowedMasks();
        BuildGridAndNeighbors();
        InitPossibilitiesAndFrontier();
    }

    public override TileBase CollapseCell(Vector2Int pos)
    {
        int idx = Idx(pos.x, pos.y);
        return CollapseCellInternal(idx);
    }

    public override void EnqueueNeighbours(Cell cell, Queue<Cell> candidates)
    {
        int idx = Idx(cell.Pos.x, cell.Pos.y);

        void EnqueueIfValid(int nIdx)
        {
            if (nIdx < 0) return;
            var n = _cells[nIdx];
            if (n.Collapsed) return;
            if (n.InQueue) return;
            n.InQueue = true;
            candidates.Enqueue(n);
        }

        EnqueueIfValid(_neighN[idx]);
        EnqueueIfValid(_neighS[idx]);
        EnqueueIfValid(_neighW[idx]);
        EnqueueIfValid(_neighE[idx]);
    }


    public override Cell GetNext()
    {
        for (int c = 1; c <= _P; c++)
        {
            var q = _byCardinality[c];
            while (q.Count > 0)
            {
                int idx = q.Dequeue();
                if (_collapsedId[idx] >= 0) continue;
                if (_count[idx] != c) continue;
                return _cells[idx];
            }
        }

        return null;
    }


    public override EntropyResult ReduceByNeighbors(Cell cell)
    {
        int idx = Idx(cell.Pos.x, cell.Pos.y);
        if (_collapsedId[idx] >= 0) return new EntropyResult(_count[idx], _count[idx]);

        int before = _count[idx];
        if (before == 0) return new EntropyResult(0, 0);

        int baseWord = idx * _wordsPerMask;
        ulong oldHash = HashMask(_poss, baseWord, _wordsPerMask);

        IntersectWithNeighborMask(idx, baseWord, DIR_N, DIR_S);
        IntersectWithNeighborMask(idx, baseWord, DIR_S, DIR_N);
        IntersectWithNeighborMask(idx, baseWord, DIR_W, DIR_E);
        IntersectWithNeighborMask(idx, baseWord, DIR_E, DIR_W);

        int after = _count[idx] = CountMask(_poss, baseWord, _wordsPerMask);

        if (after == 0) return new EntropyResult(before, 0);

        if (after != before)
        {
            _byCardinality[after].Enqueue(idx);

            ulong newHash = HashMask(_poss, baseWord, _wordsPerMask);
        }

        return new EntropyResult(before, after);
    }

    private void BuildPatternsAndWeights()
    {
        var kept = new List<PatternFrequency>(patterns.Count);
        for (int i = 0; i < patterns.Count; i++)
            if (patterns[i].pattern != null && patterns[i].count >= minPatternFrequency)
                kept.Add(patterns[i]);

        _P = kept.Count;
        if (_P <= 0) throw new Exception("No patterns available after trimming.");

        _patternToID = new Dictionary<Pattern, int>(_P);
        _patternsByID = new Pattern[_P];
        _weightsByPatternID = new int[_P];
        _weightLogsByPatternID = new double[_P];

        for (int i = 0; i < _P; i++)
        {
            var pf = kept[i];
            _patternToID[pf.pattern] = i;
            _patternsByID[i] = pf.pattern;

            int w = Mathf.Max(1, pf.count);
            _weightsByPatternID[i] = w;
            _weightLogsByPatternID[i] = w * Math.Log(w);
        }

        _wordsPerMask = (_P + 63) >> 6;
    }

    private void BuildAllowedMasks()
    {
        _allowedMasks = new ulong[4][][];
        for (int d = 0; d < 4; d++)
        {
            _allowedMasks[d] = new ulong[_P][];
            for (int s = 0; s < _P; s++)
            {
                _allowedMasks[d][s] = new ulong[_wordsPerMask];
            }
        }

        foreach (var pa in compatibilities)
        {
            if (pa.source == null) continue;
            if (!_patternToID.TryGetValue(pa.source, out int s)) continue;

            foreach (var e in pa.edges)
            {
                if (e.target == null) continue;
                if (!_patternToID.TryGetValue(e.target, out int t)) continue;

                int dir = DirIndex(e.direction);
                if (dir < 0) continue;

                SetBit(_allowedMasks[dir][s], t);
            }
        }

        _unionCache = new Dictionary<UnionKey, ulong[]>(capacity: 4096);
    }

    private void BuildGridAndNeighbors()
    {
        int n = _cellCount;

        _cells = new Cell[n];
        _inQueue = new bool[n];
        _collapsedId = new int[n];
        _count = new int[n];

        _neighN = new int[n];
        _neighS = new int[n];
        _neighW = new int[n];
        _neighE = new int[n];

        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                int idx = Idx(x, y);
                _cells[idx] = new Cell(new Vector2Int(x, y));
                _collapsedId[idx] = -1;

                _neighN[idx] = (y + 1 < _height) ? Idx(x, y + 1) : -1;
                _neighS[idx] = (y - 1 >= 0) ? Idx(x, y - 1) : -1;
                _neighW[idx] = (x - 1 >= 0) ? Idx(x - 1, y) : -1;
                _neighE[idx] = (x + 1 < _width) ? Idx(x + 1, y) : -1;
            }
    }

    private void InitPossibilitiesAndFrontier()
    {
        _poss = new ulong[_cellCount * _wordsPerMask];

        for (int i = 0; i < _cellCount; i++)
        {
            int baseW = i * _wordsPerMask;
            SetAllTrue(_poss, baseW, _wordsPerMask, _P);
            _count[i] = _P;
        }

        _byCardinality = new Queue<int>[_P + 1];
        for (int c = 0; c <= _P; c++) _byCardinality[c] = new Queue<int>(capacity: 64);

        for (int i = 0; i < _cellCount; i++) _byCardinality[_count[i]].Enqueue(i);
    }

    private TileBase CollapseCellInternal(int idx)
    {
        int baseW = idx * _wordsPerMask;

        int choice = PickWeightedId(_poss, baseW, _wordsPerMask, _weightsByPatternID);
        if (choice < 0) throw new Exception("Collapse requested on empty mask.");

        ZeroMask(_poss, baseW, _wordsPerMask);
        SetBit(_poss, baseW, choice);
        _count[idx] = 1;
        _collapsedId[idx] = choice;

        return _patternsByID[choice].Tiles[0];
    }

    private void EnqueueIfValid(int neighIdx, Queue<Cell> q)
    {
        if (neighIdx < 0) return;
        if (_collapsedId[neighIdx] >= 0) return;
        if (_inQueue[neighIdx]) return;
        _inQueue[neighIdx] = true;
        q.Enqueue(_cells[neighIdx]);
    }

    private void IntersectWithNeighborMask(int idxMe, int baseWordMe, int dirToNeigh, int neighDirToMe)
    {
        int nIdx =
            (dirToNeigh == DIR_N) ? _neighN[idxMe] :
            (dirToNeigh == DIR_S) ? _neighS[idxMe] :
            (dirToNeigh == DIR_W) ? _neighW[idxMe] :
            _neighE[idxMe];

        if (nIdx < 0) return;

        int neighCollapsed = _collapsedId[nIdx];
        if (neighCollapsed >= 0)
        {
            var mask = _allowedMasks[neighDirToMe][neighCollapsed];
            AndInto(_poss, baseWordMe, mask, _wordsPerMask);
            return;
        }

        int baseWN = nIdx * _wordsPerMask;
        ulong hash = HashMask(_poss, baseWN, _wordsPerMask);
        var key = new UnionKey { dir = (byte)neighDirToMe, hash = hash };

        if (!_unionCache.TryGetValue(key, out var union))
        {
            union = new ulong[_wordsPerMask];
            for (int w = 0; w < _wordsPerMask; w++)
            {
                ulong neighWord = _poss[baseWN + w];
                while (neighWord != 0UL)
                {
                    int bit = BitOperations_TrailingZeroCount(neighWord);
                    int pid = (w << 6) + bit;
                    if (pid < _P)
                    {
                        OrWord(union, _allowedMasks[neighDirToMe][pid], _wordsPerMask);
                    }
                    neighWord &= (neighWord - 1);
                }
            }

            _unionCache[key] = union;
        }

        AndInto(_poss, baseWordMe, union, _wordsPerMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Idx(int x, int y) => y * _staticWidth + x;
    private static int _staticWidth;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetAllTrue(ulong[] dst, int baseWord, int wordsPerMask, int P)
    {
        for (int w = 0; w < wordsPerMask; w++) dst[baseWord + w] = ~0UL;
        int rem = P & 63;
        if (rem != 0)
        {
            ulong keep = (1UL << rem) - 1UL;
            dst[baseWord + wordsPerMask - 1] &= keep;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZeroMask(ulong[] dst, int baseWord, int wordsPerMask)
    {
        for (int w = 0; w < wordsPerMask; w++) dst[baseWord + w] = 0UL;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetBit(ulong[] dst, int baseWord, int bitIndex)
    {
        int w = bitIndex >> 6;
        int b = bitIndex & 63;
        dst[baseWord + w] |= (1UL << b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetBit(ulong[] mask, int bitIndex)
    {
        int w = bitIndex >> 6;
        int b = bitIndex & 63;
        mask[w] |= (1UL << b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AndInto(ulong[] me, int baseWordMe, ulong[] other, int wordsPerMask)
    {
        for (int w = 0; w < wordsPerMask; w++)
            me[baseWordMe + w] &= other[w];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OrWord(ulong[] acc, ulong[] add, int wordsPerMask)
    {
        for (int w = 0; w < wordsPerMask; w++)
            acc[w] |= add[w];
    }

    private static int CountMask(ulong[] me, int baseWord, int wordsPerMask)
    {
        int c = 0;
        for (int w = 0; w < wordsPerMask; w++)
            c += PopCount64(me[baseWord + w]);
        return c;
    }

    private static int PickWeightedId(ulong[] me, int baseWord, int wordsPerMask, int[] weights)
    {
        int total = 0;
        // sum weights
        for (int w = 0; w < wordsPerMask; w++)
        {
            ulong word = me[baseWord + w];
            while (word != 0UL)
            {
                int bit = BitOperations_TrailingZeroCount(word);
                int id = (w << 6) + bit;
                total += (id < weights.Length) ? weights[id] : 0;
                word &= (word - 1);
            }
        }
        if (total <= 0) return -1;

        int r = UnityEngine.Random.Range(0, total);
        for (int w = 0; w < wordsPerMask; w++)
        {
            ulong word = me[baseWord + w];
            while (word != 0UL)
            {
                int bit = BitOperations_TrailingZeroCount(word);
                int id = (w << 6) + bit;
                int wgt = (id < weights.Length) ? weights[id] : 0;
                if (r < wgt) return id;
                r -= wgt;
                word &= (word - 1);
            }
        }
        return -1;
    }

    private static ulong HashMask(ulong[] me, int baseWord, int wordsPerMask)
    {
        ulong h = 1469598103934665603UL;
        for (int w = 0; w < wordsPerMask; w++)
        {
            h ^= me[baseWord + w];
            h *= 1099511628211UL;
        }
        return (h == 0UL) ? 1UL : h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DirIndex(Direction d)
    {
        return d switch
        {
            Direction.NORTH => DIR_N,
            Direction.SOUTH => DIR_S,
            Direction.WEST => DIR_W,
            Direction.EAST => DIR_E,
            _ => -1
        };
    }

    private double ComputeEntropy(Cell cell)
    {
        const double EntropyNoise = 1e-9;

        int idx = Idx(cell.Pos.x, cell.Pos.y);
        if (_collapsedId[idx] >= 0) return 0.0;

        double W = 0.0;
        double Wlog = 0.0;
        int baseW = idx * _wordsPerMask;

        for (int w = 0; w < _wordsPerMask; w++)
        {
            ulong word = _poss[baseW + w];
            while (word != 0UL)
            {
                int bit = BitOperations_TrailingZeroCount(word);
                int id = (w << 6) + bit;
                if (id < _P)
                {
                    double ww = _weightsByPatternID[id];
                    W += ww;
                    Wlog += _weightLogsByPatternID[id];
                }
                word &= (word - 1);
            }
        }

        if (W <= 0.0) return double.NegativeInfinity;
        double H = Math.Log(W) - (Wlog / W);
        H += EntropyNoise * _random.NextDouble();
        return H;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int PopCount64(ulong x)
    {
        x -= (x >> 1) & 0x5555_5555_5555_5555UL;
        x = (x & 0x3333_3333_3333_3333UL) + ((x >> 2) & 0x3333_3333_3333_3333UL);
        return (int)((((x + (x >> 4)) & 0x0F0F_0F0F_0F0F_0F0FUL) * 0x0101_0101_0101_0101UL) >> 56);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int BitOperations_TrailingZeroCount(ulong value)
    {
        if (value == 0UL) return 64;
        int c = 0;
        while ((value & 1UL) == 0UL) { value >>= 1; c++; }
        return c;
    }

    private void OnEnable()
    {
        _staticWidth = _width == 0 ? 1 : _width;
    }

    private void UpdateStaticWidth() { _staticWidth = _width == 0 ? 1 : _width; }
}
