using UnityEngine;

public class Cell
{
    public bool Collapsed { get; set; }
    public Vector2Int Pos { get; set; }

    public bool InQueue;

    public Cell(Vector2Int pos)
    {
        Pos = pos;
        Collapsed = false;
        InQueue = false;
    }

    public void Collapse()
    {
        Collapsed = true;
    }
}
