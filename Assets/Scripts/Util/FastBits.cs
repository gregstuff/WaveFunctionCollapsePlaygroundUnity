using System.Runtime.CompilerServices;

static class FastBits
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(ulong x)
    {
        // Kernighan’s method: counts set bits, branch-light and fast enough
        int c = 0;
        while (x != 0) { x &= (x - 1); c++; }
        return c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(ulong x)
    {
        if (x == 0) return 64;
        int n = 0;
        // Unrolled-ish simple loop; fine for WFC usage
        while ((x & 1UL) == 0) { x >>= 1; n++; }
        return n;
    }
}
