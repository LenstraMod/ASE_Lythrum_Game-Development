using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// Menu: Lythrum > Setup > Import Pack Tiles
// Turns the repacked art sheets (Tools/MapGen/pack_desert.py and pack_dungeon.py) into sprites and Tile assets named
// <Sheet>_<index>, the same way TilesetFloor / TilesetHouse are used by the map builder:
//   d<index> -> Assets/TileSets/DesertPalette/TilesetDesert_<index>.asset   (Desert Ruins pack)
//   k<index> -> Assets/TileSets/DungeonPalette/TilesetDungeon_<index>.asset  (0x72 DungeonTileset II)
//   m<index> -> Assets/TileSets/MeadowPalette/TilesetMeadow_<index>.asset   (trees, water, castle stone: pack_meadow.py)
// Safe to run again: it only re-slices when tiles are missing and never deletes anything.
public static class LythrumPackTiles
{
    class Sheet
    {
        public char kind;
        public string texture, cells, folder, prefix;
    }

    static readonly Sheet[] Sheets =
    {
        new Sheet { kind = 'd', texture = "Assets/TileSets/TilesetDesert.png", cells = "Assets/TileSets/TilesetDesert_cells.txt",
                    folder = "Assets/TileSets/DesertPalette", prefix = "TilesetDesert" },
        new Sheet { kind = 'k', texture = "Assets/TileSets/TilesetDungeon.png", cells = "Assets/TileSets/TilesetDungeon_cells.txt",
                    folder = "Assets/TileSets/DungeonPalette", prefix = "TilesetDungeon" },
        new Sheet { kind = 'm', texture = "Assets/TileSets/TilesetMeadow.png", cells = "Assets/TileSets/TilesetMeadow_cells.txt",
                    folder = "Assets/TileSets/MeadowPalette", prefix = "TilesetMeadow" },
    };

    const int CellSize = 16;

    // Asset path pattern for tokens starting with `kind` ({0} = sprite index), or null for an unknown kind.
    public static string TileFormat(char kind)
    {
        foreach (Sheet sheet in Sheets)
        {
            if (sheet.kind == kind) return sheet.folder + "/" + sheet.prefix + "_{0}.asset";
        }
        return null;
    }

    [MenuItem("Lythrum/Setup/Import Pack Tiles")]
    static void ImportMenu()
    {
        if (EnsureTiles(force: true)) Debug.Log("[Pack Tiles] Ready.");
    }

    // Called by the map builder before it builds a map. Returns false if a sheet couldn't be imported.
    public static bool EnsureTiles(bool force = false)
    {
        bool ok = true;
        foreach (Sheet sheet in Sheets)
        {
            if (!File.Exists(sheet.texture) || !File.Exists(sheet.cells)) continue;   // sheet not generated yet

            List<int[]> cells = ReadCells(sheet);
            if (!force && AllTilesExist(sheet, cells)) continue;

            AssetDatabase.ImportAsset(sheet.texture, ImportAssetOptions.ForceSynchronousImport);
            if (!SliceTexture(sheet, cells)) { ok = false; continue; }
            CreateTileAssets(sheet, cells);
        }
        return ok;
    }

    static List<int[]> ReadCells(Sheet sheet)
    {
        List<int[]> cells = new List<int[]>();
        foreach (string line in File.ReadAllLines(sheet.cells))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            string[] parts = line.Split(' ');
            cells.Add(new[] { int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]) });
        }
        return cells;
    }

    static string TilePath(Sheet sheet, int index) => sheet.folder + "/" + sheet.prefix + "_" + index + ".asset";

    static bool AllTilesExist(Sheet sheet, List<int[]> cells)
    {
        foreach (int[] cell in cells)
        {
            if (AssetDatabase.LoadAssetAtPath<TileBase>(TilePath(sheet, cell[2])) == null) return false;
        }
        return true;
    }

    static bool SliceTexture(Sheet sheet, List<int[]> cells)
    {
        TextureImporter importer = AssetImporter.GetAtPath(sheet.texture) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[Pack Tiles] " + sheet.texture + " is not a texture.");
            return false;
        }

        // Pixel art settings, same as the other tilesets.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = CellSize;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        ISpriteFrameEditCapability capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
        {
            Debug.LogError("[Pack Tiles] The importer can't create sprites, aborting: " + sheet.texture);
            return false;
        }

        ITextureDataProvider textureProvider = provider.GetDataProvider<ITextureDataProvider>();
        textureProvider.GetTextureActualWidthAndHeight(out int width, out int height);

        Dictionary<string, SpriteRect> byName = new Dictionary<string, SpriteRect>();
        foreach (SpriteRect rect in provider.GetSpriteRects()) byName[rect.name] = rect;

        List<SpriteRect> rects = new List<SpriteRect>();
        foreach (int[] cell in cells)
        {
            string name = sheet.prefix + "_" + cell[2];
            SpriteRect rect;
            if (!byName.TryGetValue(name, out rect))
            {
                rect = new SpriteRect { name = name, spriteID = GUID.Generate() };
            }
            // Unity's y axis starts at the bottom of the texture, the sheet's rows start at the top.
            rect.rect = new Rect(cell[0] * CellSize, height - (cell[1] + 1) * CellSize, CellSize, CellSize);
            rect.alignment = SpriteAlignment.Center;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rects.Add(rect);
        }
        provider.SetSpriteRects(rects.ToArray());

        ISpriteNameFileIdDataProvider nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameProvider != null)
        {
            List<SpriteNameFileIdPair> pairs = new List<SpriteNameFileIdPair>();
            foreach (SpriteRect rect in rects) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
            nameProvider.SetNameFileIdPairs(pairs);
        }

        provider.Apply();
        importer.SaveAndReimport();
        return true;
    }

    static void CreateTileAssets(Sheet sheet, List<int[]> cells)
    {
        if (!AssetDatabase.IsValidFolder(sheet.folder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(sheet.folder).Replace('\\', '/'), Path.GetFileName(sheet.folder));
        }

        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sheet.texture))
        {
            if (asset is Sprite sprite) sprites[sprite.name] = sprite;
        }

        int created = 0;
        foreach (int[] cell in cells)
        {
            string path = TilePath(sheet, cell[2]);
            if (!sprites.TryGetValue(sheet.prefix + "_" + cell[2], out Sprite sprite))
            {
                Debug.LogWarning("[Pack Tiles] No sprite for " + sheet.prefix + " cell " + cell[0] + "," + cell[1]);
                continue;
            }

            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.colliderType = Tile.ColliderType.Sprite;   // solid tiles follow the sprite's outline, like the other sets
                AssetDatabase.CreateAsset(tile, path);
                created++;
            }
            tile.sprite = sprite;
            EditorUtility.SetDirty(tile);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Pack Tiles] " + sheet.prefix + ": " + cells.Count + " tiles ready (" + created + " new).");
    }
}
