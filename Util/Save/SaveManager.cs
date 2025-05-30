using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        public uint TilePartitionCount { get; set; }
        public uint TotalTileSlots { get; set; }
        public uint TileSize { get; set; }
        public uint BorderSize { get; set; }

        [JsonIgnore]
        public readonly uint TotalLods => (uint)Math.Log2(TilePartitionCount) + 1;


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


    public enum SaveDataIdentifier
    {
        ROOT_SAVE_DIRECTORY,
        BASE_ALBEDO,
        BASE_HEIGHT_MAP,

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

    public async static void WriteNewSave(string saveName, Image albedo, Image heightmap, int tilePartitionCount, int totalTileSlots)
    {
        // string baseDirectory = $"user://Saves//{saveName}";
        // string baseImagesDir = $"{baseDirectory}//Base Images";
        // string thumbnailsDir = $"{baseDirectory}//Thumbnails";
        // string tileDir = $"{baseDirectory}//Tiles";
        // string cubeMapDir = $"{baseDirectory}//Cube Maps";

        // string baseAlbedo = $"{baseImagesDir}//Albedo.png";
        // string baseHeightmap = $"{baseImagesDir}//Heightmap.png";
        // string thumbnailAlbedo = $"{thumbnailsDir}//Albedo Thumbnail.png";
        // string thumbnailHeightmap = $"{thumbnailsDir}//Heightmap Thumbnail.png";

        // string cubeAlbedoDir = $"{cubeMapDir}//Albedo";
        // string cubeHeightmapDir = $"{cubeMapDir}//Heightmap";

        // string tileAlbedoDir = $"{tileDir}//Albedo";
        // string tileHeightmapDir = $"{tileDir}//Heightmap";
        // int tileSize = 8192 / tilePartitionCount;

        // WorldSave worldSave = new()
        // {
        //     BaseDirectory = baseDirectory,
        //     BaseAlbedo = baseAlbedo,
        //     BaseHeightmap = baseHeightmap,
        //     ThumbnailAlbedo = thumbnailAlbedo,
        //     ThumbnailHeightmap = thumbnailHeightmap,
        //     TilesAlbedo = tileAlbedoDir,
        //     TilesHeightmap = tileHeightmapDir,
        //     TilePartitionCount = (uint)tilePartitionCount,
        //     TotalTileSlots = (uint)totalTileSlots,
        //     TileSize = (uint)tileSize,
        //     BorderSize = 0u,
        // };

        // DirAccess.MakeDirRecursiveAbsolute(baseDirectory);
        // DirAccess.MakeDirRecursiveAbsolute(baseImagesDir);
        // DirAccess.MakeDirRecursiveAbsolute(thumbnailsDir);

        // DirAccess.MakeDirRecursiveAbsolute(cubeAlbedoDir);
        // DirAccess.MakeDirRecursiveAbsolute(cubeHeightmapDir);

        // DirAccess.MakeDirRecursiveAbsolute(tileAlbedoDir);
        // DirAccess.MakeDirRecursiveAbsolute(tileHeightmapDir);

        // albedo.SavePng(baseAlbedo);
        // heightmap.SavePng(baseHeightmap);

        // GenerateThumbnail(albedo).SavePng(thumbnailAlbedo);
        // GenerateThumbnail(heightmap).SavePng(thumbnailHeightmap);

        ChunkManager chunkManager = new();
        Image[] albedoCubeMap = ChunkManager.GenerateCubeMapFromImage(albedo);
        // Image[] heightmapCubeMap = chunkManager.GenerateCubeMapFromImage(heightmap);
        // for (int i = 0; i < 6; i++)
        {
            // albedoCubeMap[i].SavePng($"{cubeAlbedoDir}//Albedo-{i}.png");
            // heightmapCubeMap[i].SavePng($"{cubeHeightmapDir}//Heightmap-{i}.png");
        }
        // chunkManager.CleanupGPUResources();

        // chunkManager.GenerateImageChunkFromCubeMap((int)worldSave.TileSize, 0, albedoCubeMap, tileAlbedoDir);
        // chunkManager.GenerateImageChunkFromCubeMap((int)worldSave.TileSize, 0, heightmapCubeMap, tileHeightmapDir);

        // await chunkManager.CreateChunks();

        // if (!Saves.TryAdd(saveName, worldSave))
        // {
        //     GD.Print("[SaveManager:168] Overriding save");
        //     Saves[saveName] = worldSave;
        // }
        // WriteSaves();

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

            if (FileExists(path))
            {
                images[thumbnail] = ImageTexture.CreateFromImage(Image.LoadFromFile(path));
            }
            else
            {
                images[thumbnail] = new PlaceholderTexture2D();
            }
        }
        return images;
    }
}