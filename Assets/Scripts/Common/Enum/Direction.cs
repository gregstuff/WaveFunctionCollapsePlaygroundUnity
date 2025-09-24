using UnityEngine;

public enum Direction
{
    NONE,
    NORTH,
    SOUTH,
    EAST,
    WEST
}

public static class DirectionExtensions
{

    public static readonly Direction[] Cardinal =
    {
        Direction.NORTH,
        Direction.SOUTH,
        Direction.EAST,
        Direction.WEST
    };

    public static Vector2Int ToVector(this Direction dir)
    {
        return dir switch
        {
            Direction.NORTH => new Vector2Int(0, -1),
            Direction.SOUTH => new Vector2Int(0, 1),
            Direction.WEST => new Vector2Int(1, 0),
            Direction.EAST => new Vector2Int(-1, 0),
            _ => Vector2Int.zero
        };
    }

    public static Direction GetOpposite(this Direction dir)
    {
        return dir switch
        {
            Direction.NORTH => Direction.SOUTH,
            Direction.SOUTH => Direction.NORTH,
            Direction.WEST => Direction.EAST,
            Direction.EAST => Direction.WEST,
            _ => Direction.NONE
        };
    }
}