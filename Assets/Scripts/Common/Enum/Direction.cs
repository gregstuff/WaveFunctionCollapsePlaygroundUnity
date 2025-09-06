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
    public static Vector2Int ToVector(this Direction dir)
    {
        return dir switch
        {
            Direction.NORTH => Vector2Int.up,
            Direction.SOUTH => Vector2Int.down,
            Direction.WEST => Vector2Int.left,
            Direction.EAST => Vector2Int.right,
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