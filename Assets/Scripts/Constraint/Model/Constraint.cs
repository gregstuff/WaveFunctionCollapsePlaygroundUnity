using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class Constraint
{
    [SerializeField] public TileBase Tile;
    [SerializeField] public DirectionToTiles[] DirectionsToTiles;
}
