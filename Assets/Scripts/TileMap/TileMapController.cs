using System.Collections.Generic;
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

    private readonly Queue<PaintOp> _paintOps = new();

    private void Start()
    {
        if (constraintModel == null)
        {
            EditorUtility.DisplayDialog("Error", "You need to select a valid Constraints Model", "Okay");
            return;
        }

        InitializeGridCells();
        constraintModel.Init(dimensions);
        tilemapResolver.ResolveTilemap(constraintModel, OnCellChanged);
        StartCoroutine(DrawRoutine());
    }

    private void OnCellChanged(CollapseUpdate u)
    {
        // SNAPSHOT: copy positions+tiles now, while u’s data is still valid
        var r = u.OutputRect; // absolute rect
        int w = r.width, h = r.height, count = w * h;

        var positions = new Vector3Int[count];
        var tiles = new TileBase[count];

        int i = 0;
        for (int y = r.yMin; y < r.yMax; ++y)
            for (int x = r.xMin; x < r.xMax; ++x)
            {
                positions[i] = new Vector3Int(x, y, 0);

                // If TileAt expects ABSOLUTE coords (your current working code), keep this:
                tiles[i] = u.TileAt(new Vector2Int(x, y));

                // If TileAt expects LOCAL coords inside the rect, use this instead:
                // tiles[i] = u.TileAtLocal(x - r.xMin, y - r.yMin);

                i++;
            }

        _paintOps.Enqueue(new PaintOp(positions, tiles));
    }

    private System.Collections.IEnumerator DrawRoutine()
    {
        while (true)
        {
            while (_paintOps.Count > 0)
            {
                var op = _paintOps.Dequeue();

                // Fast path: apply the snapshot we created in OnCellChanged
                // (You can switch to SetTilesBlock if you prefer blocks.)
                for (int i = 0; i < op.Positions.Length; i++)
                    tileMap.SetTile(op.Positions[i], op.Tiles[i]);
            }
            yield return null;
        }
    }

    private void InitializeGridCells()
    {
        var defaultTile = constraintModel.DefaultTile;
        for (int y = 0; y < dimensions.y; ++y)
            for (int x = 0; x < dimensions.x; ++x)
                tileMap.SetTile(new Vector3Int(x, y, 0), defaultTile);
    }
}
