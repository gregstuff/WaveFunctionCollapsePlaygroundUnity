using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SimpleTileConstraintModelSO : ConstraintModelSO
{

    [Header("Tile Data")]
    [SerializeField] public Constraint[] Constraints;

    private Dictionary<TileBase, Dictionary<Direction, List<TileBase>>> _tilesToDirectionsToTiles;

    #region WFCData
    private Dictionary<Vector2Int, Cell> _cells;
    private Dictionary<Vector2Int, TileBase> _resolvedTiles;
    private Dictionary<Vector2Int, HashSet<TileBase>> _possibleTilesForCells;
    private Dictionary<Vector2Int, Dictionary<Direction, Cell>> _neighboursForPos;
    private List<HashSet<Cell>> _buckets;
    private int _maxEntropy;
    private Vector2Int _dimensions;
    #endregion

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

    private void InitTilesToDirectionsToTiles()
    {
        _tilesToDirectionsToTiles = new Dictionary<TileBase, Dictionary<Direction, List<TileBase>>>();

        foreach (var constraint in Constraints)
        {
            _tilesToDirectionsToTiles[constraint.Tile] = new Dictionary<Direction, List<TileBase>>();
            foreach (var directionToTiles in constraint.DirectionsToTiles)
            {
                _tilesToDirectionsToTiles[constraint.Tile][directionToTiles.Dir] = directionToTiles.Tiles.ToList();
            }
        }
    }

    public override void Init(Vector2Int dimensions)
    {
        _dimensions = dimensions;

        _cells = new();
        _resolvedTiles = new();
        _possibleTilesForCells = new();
        _neighboursForPos = new();
        _buckets = new();

        InitTilesToDirectionsToTiles();
        InitRelationshipsAndBuckets();
    }

    private void InitRelationshipsAndBuckets()
    {
        var possibleTiles = Constraints.Select(constraint => constraint.Tile).ToHashSet();
        _maxEntropy = possibleTiles.Count;
        _buckets = new List<HashSet<Cell>>(_maxEntropy + 1);

        // cell lookup by entropy
        for (int i = 0; i <= _maxEntropy; i++)
            _buckets.Add(new HashSet<Cell>());

        for (int y = 0; y < _dimensions.y; ++y)
        {
            for (int x = 0; x < _dimensions.x; ++x)
            {
                // initialize cells
                var pos = new Vector2Int(x, y);
                var cell = new Cell(pos);
                _cells.Add(pos, cell);
                _possibleTilesForCells.Add(pos, new HashSet<TileBase>(possibleTiles));
                //init entropy bucket for cell
                _buckets[_maxEntropy].Add(cell);
            }
        }

        // after cell init, go back over to establish relationships
        for (int y = 0; y < _dimensions.y; ++y)
        {
            for (int x = 0; x < _dimensions.x; ++x)
            {
                var pos = new Vector2Int(x, y);

                // initialize neighbour relationships
                var northCellPos = pos + Direction.NORTH.ToVector();
                var southCellPos = pos + Direction.SOUTH.ToVector();
                var westCellPos = pos + Direction.WEST.ToVector();
                var eastCellPos = pos + Direction.EAST.ToVector();

                _cells.TryGetValue(northCellPos, out var northCell);
                _cells.TryGetValue(southCellPos, out var southCell);
                _cells.TryGetValue(westCellPos, out var westCell);
                _cells.TryGetValue(eastCellPos, out var eastCell);

                _neighboursForPos.Add(pos, new());
                _neighboursForPos[pos].Add(Direction.NORTH, northCell);
                _neighboursForPos[pos].Add(Direction.SOUTH, southCell);
                _neighboursForPos[pos].Add(Direction.WEST, westCell);
                _neighboursForPos[pos].Add(Direction.EAST, eastCell);
            }
        }

    }

    public override TileBase CollapseCell(Vector2Int pos)
    {
        var cell = _cells[pos];
        var set = _possibleTilesForCells[pos];

        var resolvedTile = set.ElementAt(UnityEngine.Random.Range(0, set.Count));

        _resolvedTiles[pos] = resolvedTile;

        var before = set.Count;
        if (before != 1) _buckets[before].Remove(cell);

        set.Clear();
        set.Add(resolvedTile);

        cell.Collapse();
        _buckets[1].Add(cell);

        return resolvedTile;
    }


    public override Cell GetNext()
    {
        Cell target = null;
        for (int e = 1; e < _buckets.Count; e++)
        {
            if (_buckets[e].Count > 0)
            {
                var randIndex = UnityEngine.Random.Range(0, _buckets[e].Count);
                target = _buckets[e].ElementAt(randIndex);
                _buckets[e].Remove(target);
                break;
            }
        }
        return target;
    }

    public override void EnqueueNeighbours(Cell c, Queue<Cell> candidates)
    {
        var neighboursMap = _neighboursForPos[c.Pos];
        foreach (var kvp in neighboursMap)
        {
            var neighbour = kvp.Value;
            if (neighbour != null && !neighbour.Collapsed && !candidates.Contains(neighbour)) candidates.Enqueue(neighbour);
        }
    }

    public override EntropyResult ReduceByNeighbors(Cell cell)
    {
        void HandleCollapsedNeighbour(Direction neighborDirToMe, Cell neighbour)
        {
            var tile = _resolvedTiles[neighbour.Pos];
            var possibleTiles = _possibleTilesForCells[cell.Pos];

            var neighborDict = _tilesToDirectionsToTiles[tile];
            var allowed = neighborDict[neighborDirToMe];

            possibleTiles.RemoveWhere(t => !allowed.Contains(t));
        }

        void HandleUncollapsedNeighbour(Direction myDirectionToNeighbour, Cell neighbour)
        {
            var mySet = _possibleTilesForCells[cell.Pos];
            var neighSet = _possibleTilesForCells[neighbour.Pos];

            TileBase[] toRemove = null; int ptr = 0;

            foreach (var t in mySet)
            {
                var neighborOptionsAllowedByMe = _tilesToDirectionsToTiles[t][myDirectionToNeighbour];
                var canHappen = neighborOptionsAllowedByMe.Any(neighSet.Contains);
                if (!canHappen) (toRemove ??= new TileBase[mySet.Count])[ptr++] = t;
            }

            for (int i = 0; i < ptr; ++i) mySet.Remove(toRemove[i]);
        }

        var possibleTiles = _possibleTilesForCells[cell.Pos];

        int before = possibleTiles.Count;

        foreach (var (dirToNeighbour, neighbour) in _neighboursForPos[cell.Pos])
        {
            if (neighbour == null) continue;

            var myDirToNeighbor = dirToNeighbour;
            var neighborDirToMe = dirToNeighbour.GetOpposite();

            if (neighbour.Collapsed) HandleCollapsedNeighbour(neighborDirToMe, neighbour);
            else HandleUncollapsedNeighbour(myDirToNeighbor, neighbour);
        }

        int after = possibleTiles.Count;

        if (before != after)
        {
            _buckets[before].Remove(cell);
            _buckets[after].Add(cell);
        }

        return new EntropyResult(before, after);
    }


}
