using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PopulatedTileMap : MonoBehaviour
{

    public FlatTileResult GetFlatTiles()
    {
        if (!TryGetComponent<Tilemap>(out var tilemap)) throw new Exception($"{typeof(PopulatedTileMap)} needs a game object with a tilemap");

        tilemap.CompressBounds();
        var cellBounds = tilemap.cellBounds;
        var tiles = tilemap.GetTilesBlock(cellBounds);
        var uniqueTiles = tiles.Distinct().ToList();
        return new FlatTileResult
        {
            Height = cellBounds.size.y,
            Width = cellBounds.size.x,
            Tiles = tiles,
            UniqueTiles = uniqueTiles
        };
    }

    public TileBase[,] GetTiles()
    {
        var (height, width, tiles, uniqueTiles) = GetFlatTiles();
        var tiles2D = new TileBase[height, width];
        int i = 0;

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                tiles2D[y, x] = tiles[i++];
            }
        }

        return tiles2D;
    }

    public class FlatTileResult
    {
        public int Height { get; set; }
        public int Width { get; set; }
        public TileBase[] Tiles { get; set; }
        public List<TileBase> UniqueTiles { get; set; }

        public void Deconstruct(
            out int height,
            out int width,
            out TileBase[] tiles,
            out List<TileBase> uniqueTiles)
        {
            height = Height;
            width = Width;
            tiles = Tiles;
            uniqueTiles = UniqueTiles;
        }
    }

}
