using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ConstraintModelSO : ScriptableObject
{
    [SerializeField] public Constraint[] Constraints;
    [SerializeField] public TileBase DefaultTile;

    private Dictionary<TileBase, Dictionary<Direction, List<TileBase>>> _tilesToDirectionsToTiles;

    public Dictionary<Direction, List<TileBase>> GetDirectionTilesForTile(TileBase tileBase)
    {
        EnsureTilesToDirectionsToTiles();
        return _tilesToDirectionsToTiles[tileBase];
    }

    public void BuildConstraints(Dictionary<TileBase, Dictionary<Direction, List<TileBase>>> allConstraints, TileBase defaultTile)
    {
        DefaultTile = defaultTile;
        var builtConstraints = new List<Constraint>();
        foreach (var constraintsForTile in allConstraints)
        {
            var dirTiles = from dirToTiles in constraintsForTile.Value
                           select new DirectionToTiles
                           {
                               Dir = dirToTiles.Key,
                               Tiles = dirToTiles.Value.ToArray(),
                           };

            Constraint constraint = new Constraint()
            {
                DirectionsToTiles = dirTiles.ToArray(),
                Tile = constraintsForTile.Key
            };

            builtConstraints.Add(constraint);
        }
        Constraints = builtConstraints.ToArray();
    }

    private void EnsureTilesToDirectionsToTiles()
    {
        if (_tilesToDirectionsToTiles != null) return;

        _tilesToDirectionsToTiles = new Dictionary<TileBase, Dictionary<Direction, List<TileBase>>>();


        foreach (var constraint in Constraints)
        {
            _tilesToDirectionsToTiles[constraint.Tile] = new Dictionary<Direction, List<TileBase>>();
            foreach (var directionToTiles in constraint.DirectionsToTiles)
            {
                _tilesToDirectionsToTiles[constraint.Tile][directionToTiles.Dir] = directionToTiles.Tiles.ToList();

                //Debug.Log($"tile: {constraint.Tile.name} dir: {directionToTiles.Dir} - {string.Join(',', directionToTiles.Tiles.ToArray().Select(tile => tile.name))}");
            }
        }

    }
}
