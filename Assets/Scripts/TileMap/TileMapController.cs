using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileMapController : MonoBehaviour
{
    [Header("Object References")]
    [SerializeField] private ITilemapResolver tilemapResolver;
    [SerializeField] private ConstraintModelSO constraintModel;

    [Header("Tilemap settings")]
    [SerializeField] private Tilemap tileMap;
    [SerializeField] private Vector2Int dimensions = new Vector2Int(100, 100);

    private Dictionary<Vector2Int, Cell> cells;

    private void Start()
    {
        if (constraintModel == null
            || constraintModel.Constraints == null
            || constraintModel.Constraints.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "You need to select a valid Constraints Model", "Okay");
            return;
        }

        // init to first tile
        InitializeGridCells();
        tilemapResolver.ResolveTilemap(constraintModel, cells, OnCellChanged);
    }

    public void OnCellChanged(Cell cell)
    {
        tileMap.SetTile(new Vector3Int(cell.Pos.x, cell.Pos.y), cell.Tile);
    }

    private void InitializeGridCells()
    {
        cells = new Dictionary<Vector2Int, Cell>();
        var defaultTile = constraintModel.DefaultTile;
        var possibleTiles = constraintModel.Constraints.Select(constraint => constraint.Tile);

        for (int y = 0; y < dimensions.y; ++y)
        {
            for (int x = 0; x < dimensions.x; ++x)
            {
                var pos = new Vector3Int(x, y);
                tileMap.SetTile(new Vector3Int(x, y), defaultTile);
                cells.Add((Vector2Int)pos, new Cell((Vector2Int)pos, defaultTile, possibleTiles));
            }
        }
    }


}
