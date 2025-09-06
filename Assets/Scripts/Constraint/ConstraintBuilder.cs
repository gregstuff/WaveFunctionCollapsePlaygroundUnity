using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ConstraintBuilder : MonoBehaviour
{
    [SerializeField] private Tile[] _tiles;
    [SerializeField] private Tile _defaultTile;

    [ContextMenu("Generate Constraints")]
    public void GenerateConstraints()
    {
        if (_tiles == null || _tiles.Length == 0)
        {
            UnityEditor.EditorUtility.DisplayDialog("Error", "You need tiles to generate constraints", "OK");
            return;
        }

        var tileEdges = BuildEdges();

        var compatible = BuildCompatibility(tileEdges);

        var constraintModelSO = ScriptableObject.CreateInstance<ConstraintModelSO>();
        constraintModelSO.BuildConstraints(compatible, _defaultTile);

        persistModel(constraintModelSO);

    }

    private void persistModel(ConstraintModelSO constraintModelSO)
    {
        string path = EditorUtility.SaveFilePanelInProject(
             "Save Constraint Model",
             "NewConstraintModel",
             "asset",
             "Choose a location to save the constraint model asset."
         );

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(constraintModelSO, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = constraintModelSO;
        }
    }

    private Dictionary<TileBase, Dictionary<Direction, Color32[]>> BuildEdges()
    {
        var tileEdges = new Dictionary<TileBase, Dictionary<Direction, Color32[]>>();

        foreach (var tile in _tiles)
        {
            var edges = TileEdgeUtil.ExtractEdges(tile.sprite);
            tileEdges[tile] = edges;
        }
        return tileEdges;
    }

    private Dictionary<TileBase, Dictionary<Direction, List<TileBase>>> BuildCompatibility(
        Dictionary<TileBase, Dictionary<Direction, Color32[]>> tileEdges)
    {
        var result = new Dictionary<TileBase, Dictionary<Direction, List<TileBase>>>();

        foreach (var (tileA, edgesA) in tileEdges)
        {
            var map = new Dictionary<Direction, List<TileBase>>
            {
                { Direction.NORTH, new List<TileBase>() },
                { Direction.SOUTH, new List<TileBase>() },
                { Direction.WEST,  new List<TileBase>() },
                { Direction.EAST,  new List<TileBase>() }
            };

            foreach (var (tileB, edgesB) in tileEdges)
            {
                if (TileEdgeUtil.EdgeEquals(edgesA[Direction.EAST], edgesB[Direction.WEST]))
                    map[Direction.EAST].Add(tileB);

                if (TileEdgeUtil.EdgeEquals(edgesA[Direction.WEST], edgesB[Direction.EAST]))
                    map[Direction.WEST].Add(tileB);

                if (TileEdgeUtil.EdgeEquals(edgesA[Direction.NORTH], edgesB[Direction.SOUTH]))
                    map[Direction.NORTH].Add(tileB);

                if (TileEdgeUtil.EdgeEquals(edgesA[Direction.SOUTH], edgesB[Direction.NORTH]))
                    map[Direction.SOUTH].Add(tileB);
            }

            result[tileA] = map;
        }

        return result;
    }

}
