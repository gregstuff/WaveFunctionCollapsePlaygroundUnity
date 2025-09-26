using UnityEngine;

public class Cell
{
    public bool Collapsed { get; set; }
    public Vector2Int Pos { get; set; }
    public bool InQueue;

    public Cell(Vector2Int pos)
    {
        Collapsed = false;
        InQueue = false;
        Pos = pos;
    }

    public void Collapse()
    {
        Collapsed = true;
    }
}
