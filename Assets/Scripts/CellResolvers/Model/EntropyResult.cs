
public class EntropyResult
{

    public int OldEntropy { get; }
    public int NewEntropy { get; }

    public EntropyResult(int oldEntropy, int newEntropy)
    {
        OldEntropy = oldEntropy;
        NewEntropy = newEntropy;
    }

    public bool HasDiff()
    {
        return OldEntropy != NewEntropy;
    }

    public bool NoEntropy()
    {
        return NewEntropy == 0;
    }

}
