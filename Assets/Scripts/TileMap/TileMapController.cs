using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileMapController : MonoBehaviour
{
    [Header("Object References")]
    [SerializeField] private TilemapResolver tilemapResolver;
    [SerializeField] private ConstraintModelSO constraintModel;

    [Header("Tilemap settings")]
    [SerializeField] private Tilemap tileMap;
    [SerializeField] private Vector2Int dimensions = new Vector2Int(100, 100);

    private void Start()
    {
        if (constraintModel == null)
        {
            EditorUtility.DisplayDialog("Error", "You need to select a valid Constraints Model", "Okay");
            return;
        }

        // init to first tile
        InitializeGridCells();
        constraintModel.Init(dimensions);
        tilemapResolver.ResolveTilemap(constraintModel, OnCellChanged);
    }

    private void OnCellChanged(CollapseUpdate u)
    {
        var r = u.OutputRect; // [Cell .. Cell+N)
        for (int y = r.yMin; y < r.yMax; ++y)
            for (int x = r.xMin; x < r.xMax; ++x)
            {
                var p = new Vector2Int(x, y);
                tileMap.SetTile((Vector3Int)p, u.TileAt(p));
            }
    }

    private void InitializeGridCells()
    {
        var defaultTile = constraintModel.DefaultTile;

        for (int y = 0; y < dimensions.y; ++y)
        {
            for (int x = 0; x < dimensions.x; ++x)
            {
                var pos = new Vector3Int(x, y);
                tileMap.SetTile(new Vector3Int(pos.x, pos.y), defaultTile);
            }
        }
    }


}
