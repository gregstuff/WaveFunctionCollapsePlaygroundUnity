using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class DirectionToTiles
{
    [SerializeField] public Direction Dir;
    [SerializeField] public TileBase[] Tiles;
}
