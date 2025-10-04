
using System.Runtime.CompilerServices;

public readonly struct EntropyResult
{
    public readonly int OldEntropy;
    public readonly int NewEntropy;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool HasDiff() => OldEntropy != NewEntropy;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool NoEntropy() => NewEntropy == 0;
    public EntropyResult(int oldE, int newE) { OldEntropy = oldE; NewEntropy = newE; }
}
