
using UnityEngine;
using UnityEngine.Tilemaps;

public class OverlapModelTileMapConstraintBuilder : ConstraintBuilder
{

    [Header("Object References")]
    [SerializeField] private PopulatedTileMap[] populatedTileMaps;

    [Header("Constraint Builder Controls")]
    [SerializeField] private int areaWidth;

    public override void GenerateConstraints()
    {
        foreach (var tilemap in populatedTileMaps)
        {
            var tiles = tilemap.GetTiles();



        }

    }

    private void ExtractPatternData(TileBase[,] tiles)
    {





    }


}
