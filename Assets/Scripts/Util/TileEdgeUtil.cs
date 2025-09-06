using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TileEdgeUtil
{
    public static Dictionary<Direction, Color32[]> ExtractEdges(Sprite sprite)
    {
        var tex = sprite.texture;
        Rect r = sprite.textureRect;

        int xMin = Mathf.RoundToInt(r.xMin);
        int yMin = Mathf.RoundToInt(r.yMin);
        int w = Mathf.RoundToInt(r.width);
        int h = Mathf.RoundToInt(r.height);

        if (w <= 0 || h <= 0)
        {
            return new Dictionary<Direction, Color32[]>
            {
                { Direction.NORTH, System.Array.Empty<Color32>() },
                { Direction.SOUTH, System.Array.Empty<Color32>() },
                { Direction.WEST,  System.Array.Empty<Color32>() },
                { Direction.EAST,  System.Array.Empty<Color32>() }
            };
        }

        Color[] blockF = tex.GetPixels(xMin, yMin, w, h);
        var block = new Color32[blockF.Length];
        for (int i = 0; i < blockF.Length; i++) block[i] = blockF[i];

        Color32 At(int x, int y) => block[y * w + x];

        var north = new Color32[w];
        var south = new Color32[w];
        var west = new Color32[h];
        var east = new Color32[h];

        for (int x = 0; x < w; x++) north[x] = At(x, h - 1);
        for (int x = 0; x < w; x++) south[x] = At(x, 0);
        for (int y = 0; y < h; y++) west[y] = At(0, y);
        for (int y = 0; y < h; y++) east[y] = At(w - 1, y);

        return new Dictionary<Direction, Color32[]>
        {
            { Direction.NORTH, north },
            { Direction.SOUTH, south },
            { Direction.WEST,  west  },
            { Direction.EAST,  east  }
        };
    }

    public static Dictionary<Direction, Color32[]> ExtractEdges(TileBase tile)
    {
        if (tile is Tile t && t.sprite != null)
            return ExtractEdges(t.sprite);

        return new Dictionary<Direction, Color32[]>
        {
            { Direction.NORTH, System.Array.Empty<Color32>() },
            { Direction.SOUTH, System.Array.Empty<Color32>() },
            { Direction.WEST,  System.Array.Empty<Color32>() },
            { Direction.EAST,  System.Array.Empty<Color32>() },
        };
    }

    public static bool EdgeEquals(Color32[] a, Color32[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i])) return false;
        }
        return true;
    }
}
