using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PopulatedTileMap : MonoBehaviour
{

    public TileBase[,] GetTiles()
    {
        if (!TryGetComponent<Tilemap>(out var tilemap)) throw new Exception($"{typeof(PopulatedTileMap)} needs a game object with a tilemap");

        tilemap.CompressBounds();
        var cellBounds = tilemap.cellBounds;
        var flatTileArray = tilemap.GetTilesBlock(cellBounds);
        var tiles = new TileBase[cellBounds.size.y, cellBounds.size.x];

        int i = 0;

        for (int y = 0; y < cellBounds.size.y; ++y)
        {
            for (int x = 0; x < cellBounds.size.x; ++x)
            {
                tiles[y, x] = flatTileArray[i++];
            }
        }

        return tiles;

    }

}
