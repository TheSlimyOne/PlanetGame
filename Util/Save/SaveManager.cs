using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Godot;
using PlanetGame.Rendering.VirtualTexturing;

public static class SaveManager
{
    public struct WorldSave
    {
        public string BaseDirectory { get; set; }

        public string BaseAlbedo { get; set; }
        public string BaseHeightmap { get; set; }

        public string ThumbnailAlbedo { get; set; }
        public string ThumbnailHeightmap { get; set; }

        public string TilesAlbedo { get; set; }
        public string TilesHeightmap { get; set; }
        public string TilesNormalMap { get; set; }

        public uint TotalTileSlots { get; set; }
        public uint BorderSize { get; set; }
        public uint TotalLods { get; set; }
        public uint TileSize { get; set; }
        public uint TilePadding { get; set; }

        public int[] LodToMipMap { get; set; }


        public static int GetAsInt(Dictionary<string, object> from, string key)
        {
            if (from.TryGetValue(key, out object value) && value is JsonElement element && element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32();
            }

            throw new InvalidOperationException($"Key '{key}' not found or not an integer.");
        }

        public static uint GetAsUInt(Dictionary<string, object> from, string key)
        {
            if (from.TryGetValue(key, out object value) && value is JsonElement element && element.ValueKind == JsonValueKind.Number)
            {
                return element.GetUInt32();
            }

            throw new InvalidOperationException($"Key '{key}' not found or not an integer.");
        }
    }

    public struct SavePaths
    {
        public string BaseDirectory;
        public string BaseImagesDir;
        public string ThumbnailsDir;
        public string TileDir;
        public string BaseAlbedo;
        public string BaseHeightmap;
        public string ThumbnailAlbedo;
        public string ThumbnailHeightmap;
        public string TileAlbedoDir;
        public string TileHeightmapDir;
    }
    public enum SaveDataIdentifier
    {
        ROOT_SAVE_DIRECTORY,
        BASE_ALBEDO,
        BASE_HEIGHT_MAP,
        BASE_NORMAL_MAP,

        THUMBNAIL_ALEBDO,
        THUMBNAIL_HEIGHT_MAP,

        TILE_ALBEDO,
        TILE_HEIGHT_MAP,
        TILE_NORMAL_MAP,

        SAVE_DATA,
    }

    public static SaveDataIdentifier[] Thumbnails = [
        SaveDataIdentifier.THUMBNAIL_ALEBDO,
        SaveDataIdentifier.THUMBNAIL_HEIGHT_MAP
    ];

    public static SaveDataIdentifier[] BaseImages = [
        SaveDataIdentifier.BASE_ALBEDO,
        SaveDataIdentifier.BASE_HEIGHT_MAP,
        SaveDataIdentifier.BASE_NORMAL_MAP
    ];

    public static SaveDataIdentifier[] Tiles = [
        SaveDataIdentifier.TILE_ALBEDO,
        SaveDataIdentifier.TILE_HEIGHT_MAP,
        SaveDataIdentifier.TILE_NORMAL_MAP
    ];

    public static string CurrentSave { get; set; }
    const string SAVE_PATH = "user://Saves/saves.json";
    private static readonly Dictionary<string, WorldSave> Saves = GetSaves();
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, AllowTrailingCommas = true };

    private static Dictionary<string, WorldSave> GetSaves()
    {
        FileAccess fileAccess = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Read);
        string jsonText = fileAccess.GetAsText();
        fileAccess.Close();
        return JsonSerializer.Deserialize<Dictionary<string, WorldSave>>(jsonText, Options);
    }

    private static void WriteSaves()
    {
        string jsonText = JsonSerializer.Serialize(Saves, Options);
        using FileAccess fileAccess = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Write);
        fileAccess.StoreString(jsonText);
        fileAccess.Close();
    }


    public static SavePaths CreatePaths(string saveName)
    {
        string baseDirectory = $"user://Saves/{saveName}";
        string baseImagesDir = $"{baseDirectory}/Base Images";
        string thumbnailsDir = $"{baseDirectory}/Thumbnails";
        string tileDir = $"{baseDirectory}/Tiles";

        string baseAlbedo = $"{baseImagesDir}/Albedo.png";
        string baseHeightmap = $"{baseImagesDir}/Heightmap.png";
        string thumbnailAlbedo = $"{thumbnailsDir}/Albedo Thumbnail.png";
        string thumbnailHeightmap = $"{thumbnailsDir}/Heightmap Thumbnail.png";

        string tileAlbedoDir = $"{tileDir}/Albedo";
        string tileHeightmapDir = $"{tileDir}/Heightmap";

        DirAccess.MakeDirRecursiveAbsolute(baseDirectory);
        DirAccess.MakeDirRecursiveAbsolute(baseImagesDir);
        DirAccess.MakeDirRecursiveAbsolute(thumbnailsDir);
        DirAccess.MakeDirRecursiveAbsolute(tileAlbedoDir);
        DirAccess.MakeDirRecursiveAbsolute(tileHeightmapDir);

        return new SavePaths
        {
            BaseDirectory = baseDirectory,
            BaseImagesDir = baseImagesDir,
            ThumbnailsDir = thumbnailsDir,
            TileDir = tileDir,
            BaseAlbedo = baseAlbedo,
            BaseHeightmap = baseHeightmap,
            ThumbnailAlbedo = thumbnailAlbedo,
            ThumbnailHeightmap = thumbnailHeightmap,
            TileAlbedoDir = tileAlbedoDir,
            TileHeightmapDir = tileHeightmapDir
        };
    }

    public static async Task WriteNewSave(string saveName, Image albedo, Image heightmap, int mipCount, int tilePadding, int[] lodToMipMap)
    {
        albedo.ResizeToPo2();
        if (albedo.GetSize() != heightmap.GetSize())
        {
            heightmap.Resize(albedo.GetSize().X, albedo.GetSize().Y);
        }

        SavePaths paths = CreatePaths(saveName);

        WorldSave worldSave = new()
        {
            BaseDirectory = paths.BaseDirectory,
            BaseAlbedo = paths.BaseAlbedo,
            BaseHeightmap = paths.BaseHeightmap,
            ThumbnailAlbedo = paths.ThumbnailAlbedo,
            ThumbnailHeightmap = paths.ThumbnailHeightmap,
            TilesAlbedo = paths.TileAlbedoDir,
            TilesHeightmap = paths.TileHeightmapDir,
            TotalTileSlots = TileCache.TotalTileSlots,
            BorderSize = 0u,
            TotalLods = (uint)mipCount,
            TileSize = (uint)(albedo.GetHeight() / Mathf.Pow(2, mipCount - 1)),
            TilePadding = (uint)tilePadding,
            LodToMipMap = lodToMipMap
        };

        albedo.SavePng(paths.BaseAlbedo);
        heightmap.SavePng(paths.BaseHeightmap);

        GenerateThumbnail(albedo).SavePng(paths.ThumbnailAlbedo);
        GenerateThumbnail(heightmap).SavePng(paths.ThumbnailHeightmap);

        GD.Print("Generating Albedo map");
        await TileManager.GenerateTilesAsync(albedo, tilePadding, mipCount - 1, worldSave.TilesAlbedo);

        GD.Print("Generating Heightmap");
        await TileManager.GenerateTilesAsync(heightmap, tilePadding, mipCount - 1, worldSave.TilesHeightmap);

        if (!Saves.TryAdd(saveName, worldSave))
        {
            GD.Print("[SaveManager:162] Overriding save");
            Saves[saveName] = worldSave;
        }
        WriteSaves();
    }

    public static async Task RegenerateGenerateTiles(string saveName)
    {
        WorldSave save = GetSave(saveName);
        Image albedo = Image.LoadFromFile(save.BaseAlbedo);
        Image heightmap = Image.LoadFromFile(save.BaseHeightmap);
        int mipCount = (int)save.TotalLods;
        int tilePadding = (int)save.TilePadding;

        GD.Print("Generating Albedo map");
        await TileManager.GenerateTilesAsync(albedo, tilePadding, mipCount - 1, save.TilesAlbedo);

        GD.Print("Generating Heightmap");
        await TileManager.GenerateTilesAsync(heightmap, tilePadding, mipCount - 1, save.TilesHeightmap);
    }

    private static Image GenerateThumbnail(Image originalImage)
    {
        Image thumbnail = new();
        thumbnail.CopyFrom(originalImage);
        thumbnail.Resize(512, 256);
        return thumbnail;
    }

    public static bool IsValidDirectory(string saveName, SaveDataIdentifier directory)
    {
        return DirAccess.Open(GetDirectoryPath(saveName, directory)) == null;
    }

    public static void EnsureDirectoryExists(string saveName, SaveDataIdentifier directory)
    {
        string path = GetDirectoryPath(saveName, directory);
        if (!DirectoryExist(saveName))
            DirAccess.MakeDirRecursiveAbsolute(path);
    }

    public static WorldSave GetSave(string saveName)
    {
        return Saves[saveName];
    }

    public static WorldSave GetCurrentSave()
    {
        return Saves[CurrentSave];
    }

    public static string GetSaveDirectory(string saveName)
    {
        return Saves[saveName].BaseDirectory;
    }

    public static string GetDirectoryPath(string saveName, SaveDataIdentifier directory)
    {
        var save = Saves;
        return directory switch
        {
            SaveDataIdentifier.ROOT_SAVE_DIRECTORY => save[saveName].BaseDirectory,
            SaveDataIdentifier.BASE_ALBEDO => save[saveName].BaseAlbedo,
            SaveDataIdentifier.BASE_HEIGHT_MAP => save[saveName].BaseHeightmap,
            SaveDataIdentifier.TILE_ALBEDO => save[saveName].TilesAlbedo,
            SaveDataIdentifier.TILE_HEIGHT_MAP => save[saveName].TilesHeightmap,
            SaveDataIdentifier.TILE_NORMAL_MAP => save[saveName].TilesNormalMap,
            SaveDataIdentifier.THUMBNAIL_ALEBDO => save[saveName].ThumbnailAlbedo,
            SaveDataIdentifier.THUMBNAIL_HEIGHT_MAP => save[saveName].ThumbnailHeightmap,
            _ => string.Empty
        };
    }

    public static bool DirectoryExist(string path)
    {
        return DirAccess.Open(path) != null;
    }

    public static bool FileExists(string path)
    {
        return FileAccess.FileExists(path);
    }

    public static bool SaveNameExist(string saveName)
    {
        return DirectoryExist(Saves.GetValueOrDefault(saveName).BaseDirectory);
    }

    public static string[] GetSaveNames()
    {
        return [.. GetSaves().Select(x => x.Key)];
    }

    public static Dictionary<SaveDataIdentifier, Texture2D> GetThumbnails(string saveName)
    {
        Dictionary<SaveDataIdentifier, Texture2D> images = [];

        for (int i = 0; i < Thumbnails.Length; i++)
        {
            SaveDataIdentifier thumbnail = Thumbnails[i];
            string path = GetDirectoryPath(saveName, thumbnail);

            images[thumbnail] = FileExists(path) ? ImageTexture.CreateFromImage(Image.LoadFromFile(path)) : new PlaceholderTexture2D();
        }
        return images;
    }
}