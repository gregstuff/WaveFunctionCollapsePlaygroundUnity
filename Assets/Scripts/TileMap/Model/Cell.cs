
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Cell
{
    public TileBase Tile { get; set; }
    public HashSet<TileBase> PossibleTiles { get; set; }
    public bool Collapsed { get; set; }
    public Vector2Int Pos { get; set; }

    public Cell(Vector2Int pos, TileBase tile, IEnumerable<TileBase> possibleTiles)
    {
        Tile = tile;
        Pos = pos;
        Collapsed = false;
        PossibleTiles = new HashSet<TileBase>(possibleTiles);
    }

    public void Collapse(TileBase tile)
    {
        Tile = tile;
        Collapsed = true;
        PossibleTiles.Clear();
    }
}
