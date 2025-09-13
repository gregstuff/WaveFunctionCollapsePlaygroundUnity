#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class OverlapModelValidator : MonoBehaviour
{
    [SerializeField] private OverlappingModelTileModelSO model; // assign in Inspector

    [ContextMenu("Validate Overlap Compatibilities")]
    void Validate()
    {
        int N = model.N;
        var edges = model.compatibilities.SelectMany(pa => pa.edges.Select(e => (src: pa.source, dir: e.direction, tgt: e.target))).ToList();

        int total = edges.Count;
        int bad = 0;

        var dirTotals = new System.Collections.Generic.Dictionary<Direction, int>();
        var dirBad = new System.Collections.Generic.Dictionary<Direction, int>();

        foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
        {
            dirTotals[d] = 0;
            dirBad[d] = 0;
        }

        (Pattern S, Pattern T, Direction D)? firstFail = null;

        foreach (var (S, D, T) in edges)
        {
            dirTotals[D]++;
            bool ok = IsOverlapCompatible(S, T, D, N);
            if (!ok)
            {
                dirBad[D]++;
                bad++;
                if (firstFail == null) firstFail = (S, T, D);
            }
        }

        Debug.Log($"[WFC] Checked {total} edges | invalid={bad} ({(total == 0 ? 0 : 100f * bad / total):F1}%).");
        foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
            Debug.Log($"[WFC] {d,-5}: invalid {dirBad[d]} / {dirTotals[d]} ({(dirTotals[d] == 0 ? 0 : 100f * dirBad[d] / (float)dirTotals[d]):F1}%).");

        if (firstFail != null)
        {
            var (S, T, D) = firstFail.Value;
            Debug.LogWarning($"[WFC] First failing edge: {D}. Dumping overlap…");
            DumpOverlap(S, T, D, N);
        }
    }

    static void DumpOverlap(Pattern S, Pattern T, Direction dir, int N)
    {
        string Name(TileBase t)
        {
            if (t == null) return "∅";

            var st = t as UnityEngine.Tilemaps.Tile;
            if (st != null && st.sprite != null && !string.IsNullOrEmpty(st.sprite.name))
                return st.sprite.name;
            return t.name ?? t.GetInstanceID().ToString();
        }

        if (dir == Direction.EAST)
        {
            Debug.Log("[S right | T left]");
            for (int y = N - 1; y >= 0; y--)
            {
                var s = string.Join(",", Enumerable.Range(1, N - 1).Select(x => Name(S[y, x])));
                var t = string.Join(",", Enumerable.Range(0, N - 1).Select(x => Name(T[y, x])));
                Debug.Log($"S:{s}  ||  T:{t}");
            }
        }
        else if (dir == Direction.WEST)
        {
            Debug.Log("[S left | T right]");
            for (int y = N - 1; y >= 0; y--)
            {
                var s = string.Join(",", Enumerable.Range(0, N - 1).Select(x => Name(S[y, x])));
                var t = string.Join(",", Enumerable.Range(1, N - 1).Select(x => Name(T[y, x])));
                Debug.Log($"S:{s}  ||  T:{t}");
            }
        }
        else if (dir == Direction.NORTH)
        {
            Debug.Log("[S top | T bottom]");
            for (int x = 0; x < N; x++)
                for (int y = N - 1; y >= 1; y--)
                {
                    var s = string.Join(",", Enumerable.Range(0, N).Select(x => Name(S[y, x])));
                    var t = string.Join(",", Enumerable.Range(0, N).Select(x => Name(T[y - 1, x])));
                    Debug.Log($"S:{s}  ||  T:{t}");
                }
        }
        else if (dir == Direction.SOUTH)
        {
            Debug.Log("[S bottom | T top]");
            for (int y = 0; y <= N - 2; y++)
            {
                var s = string.Join(",", Enumerable.Range(0, N).Select(x => Name(S[y, x])));
                var t = string.Join(",", Enumerable.Range(0, N).Select(x => Name(T[y + 1, x])));
                Debug.Log($"S:{s}  ||  T:{t}");
            }
        }
    }

    private bool IsOverlapCompatible(Pattern S, Pattern T, Direction dir, int N)
    {
        switch (dir)
        {
            case Direction.EAST:
                for (int y = 0; y < N; y++)
                    for (int x = 1; x < N; x++)
                        if (S[y, x] != T[y, x - 1]) return false;
                return true;

            case Direction.WEST:
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N - 1; x++)
                        if (S[y, x] != T[y, x + 1]) return false;
                return true;

            case Direction.NORTH:
                for (int y = 1; y < N; y++)
                    for (int x = 0; x < N; x++)
                        if (S[y, x] != T[y - 1, x]) return false;
                return true;

            case Direction.SOUTH:
                for (int y = 0; y < N - 1; y++)
                    for (int x = 0; x < N; x++)
                        if (S[y, x] != T[y + 1, x]) return false;
                return true;

            default:
                return false;
        }
    }

}
#endif
