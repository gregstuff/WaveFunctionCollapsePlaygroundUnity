#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RotatedTileGenerator : MonoBehaviour
{

    private static readonly string TILE_PREFIX = "tile";

    [SerializeField] private Sprite[] TileSprites;

    [ContextMenu("Generate Tiles")]
    public void TriggerGenerateTiles()
    {
        string outputFolder = GetOutputFolder();
        GenerateTilesFromSprites(outputFolder);
    }

    private string GetOutputFolder()
    {
        var output = EditorUtility.SaveFolderPanel("Choose output folder (inside Assets)", "Assets", "");

        string outputFolder = "";

        if (!string.IsNullOrEmpty(output))
        {
            if (output.StartsWith(Application.dataPath))
                outputFolder = "Assets" + output.Substring(Application.dataPath.Length);
            else
                EditorUtility.DisplayDialog("Invalid Folder",
                    "Please pick a folder inside this project's Assets directory.\nFalling back to Assets/RotatedTiles.",
                    "OK");
        }

        var ensured = EnsureFolder(outputFolder);

        if (!ensured)
        {
            EditorUtility.DisplayDialog("Invalid Folder",
                "Please pick a folder inside this project's Assets directory.\nFalling back to Assets/RotatedTiles.",
                "OK");
            return null;
        }

        return outputFolder;
    }


    public void GenerateTilesFromSprites(string outputFolder)
    {
        if (TileSprites.Count() == 0 || outputFolder == null) return;

        var result = new List<Tile>();

        foreach (var sprite in TileSprites.Where(s => s != null))
        {
            var tex = sprite.texture;
            EnsureReadable(tex);

            var srcRect = sprite.textureRect;
            var srcW = (int)srcRect.width;
            var srcH = (int)srcRect.height;

            var srcPixels = tex.GetPixels32();

            var sub = CopySubRect(srcPixels, tex.width, tex.height, (int)srcRect.x, (int)srcRect.y, srcW, srcH);

            var variants = new List<(int deg, Color32[] pixels, int w, int h)>();

            variants.Add((0, sub, srcW, srcH));

            var r90 = Rotate90CW(sub, srcW, srcH, out int w90, out int h90);
            AddIfUnique(variants, (90, r90, w90, h90));

            var r180 = Rotate180(sub, srcW, srcH);
            AddIfUnique(variants, (180, r180, srcW, srcH));

            var r270 = Rotate270CW(sub, srcW, srcH, out int w270, out int h270);
            AddIfUnique(variants, (270, r270, w270, h270));

            foreach (var (deg, pix, w, h) in variants)
            {
                var rotTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                rotTex.SetPixels32(pix);
                rotTex.Apply(false, false);

                float ppu = sprite.pixelsPerUnit;

                Vector2 pivot = new Vector2(
                    sprite.pivot.x / sprite.rect.width,
                    sprite.pivot.y / sprite.rect.height);

                var rotSprite = Sprite.Create(rotTex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);

                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = rotSprite;
                tile.name = $"{TILE_PREFIX}_{sprite.name}_{deg}";

                result.Add(tile);

                var safeBase = Sanitize($"{TILE_PREFIX}_{sprite.name}_{deg}");
                var texPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputFolder, safeBase + ".asset"));
                var tilePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputFolder, safeBase + "_Tile.asset"));

                AssetDatabase.CreateAsset(rotTex, texPath);
                AssetDatabase.AddObjectToAsset(rotSprite, texPath);
                AssetDatabase.CreateAsset(tile, tilePath);

                EditorUtility.SetDirty(rotTex);
                EditorUtility.SetDirty(rotSprite);
                EditorUtility.SetDirty(tile);

            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void AddIfUnique(List<(int deg, Color32[] pixels, int w, int h)> list,
                                    (int deg, Color32[] pixels, int w, int h) candidate)
    {
        foreach (var existing in list)
        {
            if (existing.w == candidate.w && existing.h == candidate.h &&
                PixelsEqual(existing.pixels, candidate.pixels))
            {
                return;
            }
        }
        list.Add(candidate);
    }

    private static bool PixelsEqual(Color32[] a, Color32[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a)
                return false;
        }
        return true;
    }

    private static Color32[] CopySubRect(Color32[] src, int texW, int texH, int x, int y, int w, int h)
    {
        var dst = new Color32[w * h];
        for (int row = 0; row < h; row++)
        {
            Array.Copy(src, (y + row) * texW + x, dst, row * w, w);
        }
        return dst;
    }

    private static Color32[] Rotate90CW(Color32[] src, int w, int h, out int outW, out int outH)
    {
        outW = h; outH = w;
        var dst = new Color32[outW * outH];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int srcIdx = y * w + x;
                int dx = y;
                int dy = (outH - 1) - x;
                dst[dy * outW + dx] = src[srcIdx];
            }
        return dst;
    }

    private static Color32[] Rotate180(Color32[] src, int w, int h)
    {
        var dst = new Color32[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int srcIdx = y * w + x;
                int dx = (w - 1) - x;
                int dy = (h - 1) - y;
                dst[dy * w + dx] = src[srcIdx];
            }
        return dst;
    }

    private static Color32[] Rotate270CW(Color32[] src, int w, int h, out int outW, out int outH)
    {
        outW = h; outH = w;
        var dst = new Color32[outW * outH];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int srcIdx = y * w + x;
                int dx = (outW - 1) - y;
                int dy = x;
                dst[dy * outW + dx] = src[srcIdx];
            }
        return dst;
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    private static bool EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return false;
        if (AssetDatabase.IsValidFolder(folder)) return true;

        var parts = folder.Split('/');
        var path = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{path}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(path, parts[i]);
            path = next;
        }
        return AssetDatabase.IsValidFolder(folder);
    }

    private static void EnsureReadable(Texture2D tex)
    {
        if (tex == null) return;

        string path = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(path)) return;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
#endif
