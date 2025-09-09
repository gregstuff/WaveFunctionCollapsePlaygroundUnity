using UnityEngine;

public class Cell
{
    public bool Collapsed { get; set; }
    public Vector2Int Pos { get; set; }

    public Cell(Vector2Int pos)
    {
        Pos = pos;
        Collapsed = false;
    }

    public void Collapse()
    {
        Collapsed = true;
    }
}
