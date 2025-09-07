using System.Collections.Generic;
using UnityEngine;

public class OverlappingModelTileModelSO : ScriptableObject
{

    [SerializeField] public Dictionary<Pattern, int> PatternToFrequency;
    [SerializeField] public Dictionary<Pattern, Dictionary<Direction, List<Pattern>>> AllowedPatternsForDirection;

}
