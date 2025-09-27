using System;
using System.Collections.Generic;

[Serializable]
public class PatternAdjacency
{
    public int sourcePatternId;
    public Direction direction;
    public List<int> compatiblePatternIds = new List<int>();
}