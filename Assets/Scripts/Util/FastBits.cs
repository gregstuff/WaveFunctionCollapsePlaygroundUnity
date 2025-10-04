using System.Runtime.CompilerServices;

static class FastBits
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(ulong x)
    {
        x -= (x >> 1) & 0x5555555555555555UL;
        x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
        x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        x += x >> 8; x += x >> 16; x += x >> 32;
        return (int)(x & 0x7F);
    }

    private static readonly byte[] _tzcByte = BuildTzcByte();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(ulong x)
    {
        if (x == 0) return 64;

        uint lo = (uint)x;
        if (lo != 0)
        {
            int n = _tzcByte[lo & 0xFF]; if (n != 8) return n;
            n = _tzcByte[(lo >> 8) & 0xFF]; if (n != 8) return 8 + n;
            n = _tzcByte[(lo >> 16) & 0xFF]; if (n != 8) return 16 + n;
            return 24 + _tzcByte[(lo >> 24) & 0xFF];
        }
        uint hi = (uint)(x >> 32);

        int m = _tzcByte[hi & 0xFF]; if (m != 8) return 32 + m;
        m = _tzcByte[(hi >> 8) & 0xFF]; if (m != 8) return 40 + m;
        m = _tzcByte[(hi >> 16) & 0xFF]; if (m != 8) return 48 + m;
        return 56 + _tzcByte[(hi >> 24) & 0xFF];
    }
}
