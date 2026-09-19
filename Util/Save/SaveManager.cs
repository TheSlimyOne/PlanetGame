using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering.VirtualTexturing;

public static class SaveManager
{
    #region Save Data

    public const uint VERSION = 1;

    private struct SaveData
    {
        public VirtualTextureData VirtualTextureData;
        public TessellationData TessellationData;
        public WorldData WorldData;
        public DirectoryData DirectoryData;
        public PlanetGame.Data.RenderData RenderData;
        public AtmosphereData AtmosphereData;
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

    #endregion

    #region Save Data Groups

    public static readonly SaveDataIdentifier[] Thumbnails =
    [
        SaveDataIdentifier.THUMBNAIL_ALEBDO,
        SaveDataIdentifier.THUMBNAIL_HEIGHT_MAP
    ];

    public static readonly SaveDataIdentifier[] BaseImages =
    [
        SaveDataIdentifier.BASE_ALBEDO,
        SaveDataIdentifier.BASE_HEIGHT_MAP,
        SaveDataIdentifier.BASE_NORMAL_MAP
    ];

    public static readonly SaveDataIdentifier[] Tiles =
    [
        SaveDataIdentifier.TILE_ALBEDO,
        SaveDataIdentifier.TILE_HEIGHT_MAP,
        SaveDataIdentifier.TILE_NORMAL_MAP
    ];

    #endregion

    #region Save State

    private const string SAVE_PATH = "user://Saves/saves.json";
    private const string BRUSH_SAVE_PATH = "user://brush.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        AllowTrailingCommas = true,
        IncludeFields = true
    };

    private static Dictionary<string, SaveData> Saves = GetSaves();
    private static string _currentSave;

    public static string CurrentSave
    {
        get => _currentSave;
        set
        {
            _currentSave = value;
            LoadSave(value);
        }
    }

    public static VirtualTextureData VirtualTextureData;
    public static TessellationData TessellationData;
    public static WorldData WorldData;
    public static DirectoryData DirectoryData;
    public static PlanetGame.Data.RenderData RenderData;
    public static AtmosphereData AtmosphereData;


    #endregion

    #region Save Reading And Writing

    private static Dictionary<string, SaveData> GetSaves()
    {
        if (!FileAccess.FileExists(SAVE_PATH))
            return [];

        using FileAccess fileAccess = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Read);

        string jsonText = fileAccess.GetAsText();

        if (string.IsNullOrWhiteSpace(jsonText))
            return [];

        return JsonSerializer.Deserialize<Dictionary<string, SaveData>>(jsonText, Options) ?? [];
    }

    public static void RefreshSaves()
    {
        Saves = GetSaves();
    }

    public static void WriteSaves()
    {
        string jsonText = JsonSerializer.Serialize(Saves, Options);

        using FileAccess fileAccess = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Write);
        fileAccess.StoreString(jsonText);
    }

    private static void LoadSave(string saveName)
    {
        SaveData save = Saves[saveName];

        VirtualTextureData = save.VirtualTextureData;
        TessellationData = save.TessellationData;
        WorldData = save.WorldData;
        DirectoryData = save.DirectoryData;
        RenderData = save.RenderData;
        AtmosphereData = save.AtmosphereData;
    }

    public static void SaveCurrentData(string saveName)
    {
        Saves[saveName] = new SaveData
        {
            VirtualTextureData = VirtualTextureData,
            TessellationData = TessellationData,
            WorldData = WorldData,
            DirectoryData = DirectoryData,
            RenderData = RenderData,
            AtmosphereData = AtmosphereData
        };

        WriteSaves();
    }

    public static string[] GetSaveNames()
    {
        return [.. Saves.Keys];
    }

    public static bool SaveNameExist(string saveName)
    {
        if (!Saves.TryGetValue(saveName, out SaveData save))
            return false;

        return DirectoryExist(save.DirectoryData.BaseDirectory);
    }

    #endregion

    #region Save Creation

    public static DirectoryData CreateDirectories(string saveName)
    {
        string baseDirectory = $"user://Saves/{saveName}";
        string baseImageDirectory = $"{baseDirectory}/Base Images";
        string thumbnailsDirectory = $"{baseDirectory}/Thumbnails";
        string tileDirectory = $"{baseDirectory}/Tiles";

        string baseAlbedo = $"{baseImageDirectory}/Albedo.png";
        string baseHeightmap = $"{baseImageDirectory}/Heightmap.png";

        string thumbnailAlbedo = $"{thumbnailsDirectory}/Albedo Thumbnail.png";
        string thumbnailHeightmap = $"{thumbnailsDirectory}/Heightmap Thumbnail.png";

        string tileAlbedo = $"{tileDirectory}/{GetTileFileName(TileCache.TileCacheType.ALBEDO)}";
        string tileHeightmap = $"{tileDirectory}/{GetTileFileName(TileCache.TileCacheType.HEIGHTMAP)}";

        DirAccess.MakeDirRecursiveAbsolute(baseDirectory);
        DirAccess.MakeDirRecursiveAbsolute(baseImageDirectory);
        DirAccess.MakeDirRecursiveAbsolute(thumbnailsDirectory);
        DirAccess.MakeDirRecursiveAbsolute(tileDirectory);

        return new DirectoryData
        {
            BaseDirectory = baseDirectory,

            BaseAlbedo = baseAlbedo,
            BaseHeightmap = baseHeightmap,

            ThumbnailAlbedo = thumbnailAlbedo,
            ThumbnailHeightmap = thumbnailHeightmap,

            TileAlbedo = tileAlbedo,
            TileHeightmap = tileHeightmap
        };
    }

    public static async Task WriteNewSave(string saveName, Image albedo, Image heightmap, uint[] lodToMipMap, Action<int, string, int> onProgress)
    {
        Vector2I size = new(16384, 8192);

        albedo.Resize(size.X, size.Y, Image.Interpolation.Bilinear);

        if (albedo.GetSize() != heightmap.GetSize())
            heightmap.Resize(albedo.GetSize().X, albedo.GetSize().Y);

        DirectoryData = CreateDirectories(saveName);

        VirtualTextureData = new VirtualTextureData(
            6,
            0,
            lodToMipMap,
            [
                "0_0_0_0",
                "0_1_0_0",
                "0_2_0_0",
                "0_3_0_0",
                "0_4_0_0",
                "0_5_0_0",
            ]
        );

        TessellationData = new TessellationData(
            resolution: 5,
            subFactor: 4,
            maximumLod: 12,
            minimumLod: 0,
            maximumKeys: 40000,
            cullingDepth: 1,
            cullingMargin: new Vector4(0.09f, 0.09f, 0.3f, 15)
        );

        WorldData = new WorldData(
            radius: 100,
            heightScale: 0.025f
        );

        RenderData = new PlanetGame.Data.RenderData()
        {
            IsCulling = false,
            IsMorphing = false,
            IsCube = false  
        };

        AtmosphereData = new AtmosphereData(WorldData.Radius + 10);

        _currentSave = saveName;

        albedo.SavePng(DirectoryData.BaseAlbedo);
        heightmap.SavePng(DirectoryData.BaseHeightmap);

        GenerateThumbnail(albedo).SavePng(DirectoryData.ThumbnailAlbedo);
        GenerateThumbnail(heightmap).SavePng(DirectoryData.ThumbnailHeightmap);

        SaveCurrentData(saveName);

        TileFile albedoTileFile = new(albedo.GetSize(),
            Image.Format.Rgba8,
            DirectoryData.TileAlbedo);

        TileFile heightmapTileFile = new(heightmap.GetSize(),
            Image.Format.R8,
            DirectoryData.TileHeightmap);

        GD.Print("Creating Tiles");
        albedoTileFile.OnTileGeneratedProgress += onProgress;
        try
        {
            await albedoTileFile.CreateTiles(albedo);
        }
        finally
        {
            albedoTileFile.OnTileGeneratedProgress -= onProgress;
        }


        heightmapTileFile.OnTileGeneratedProgress += onProgress;
        try
        {
            await heightmapTileFile.CreateTiles(heightmap);
        }
        finally
        {
            heightmapTileFile.OnTileGeneratedProgress -= onProgress;
        }

        GD.Print("Finished Creating Tiles");

    }

    private static string GetTileFileName(TileCache.TileCacheType tileCacheType)
    {
        string tilePrefix = TileCache.GetTileCacheTypeName(tileCacheType).ToLower();
        return $"{tilePrefix}_tiles.bin";
    }

    #endregion

    #region Tile Generation

    private static Image GenerateThumbnail(Image originalImage)
    {
        Image thumbnail = new();

        thumbnail.CopyFrom(originalImage);
        thumbnail.Resize(512, 256);

        return thumbnail;
    }

    #endregion

    #region Directory Management

    public static bool IsValidDirectory(string saveName, SaveDataIdentifier directory)
    {
        return DirectoryExist(GetDirectoryPath(saveName, directory));
    }

    public static void EnsureDirectoryExists(string saveName, SaveDataIdentifier directory)
    {
        string path = GetDirectoryPath(saveName, directory);

        if (!DirectoryExist(path))
            DirAccess.MakeDirRecursiveAbsolute(path);
    }

    public static string GetSaveDirectory(string saveName)
    {
        return Saves[saveName].DirectoryData.BaseDirectory;
    }

    public static string GetDirectoryPath(string saveName, SaveDataIdentifier directory)
    {
        return directory switch
        {
            SaveDataIdentifier.ROOT_SAVE_DIRECTORY => Saves[saveName].DirectoryData.BaseDirectory,

            SaveDataIdentifier.BASE_ALBEDO => Saves[saveName].DirectoryData.BaseAlbedo,
            SaveDataIdentifier.BASE_HEIGHT_MAP => Saves[saveName].DirectoryData.BaseHeightmap,

            SaveDataIdentifier.TILE_ALBEDO => Saves[saveName].DirectoryData.TileAlbedo,
            SaveDataIdentifier.TILE_HEIGHT_MAP => Saves[saveName].DirectoryData.TileHeightmap,
            SaveDataIdentifier.TILE_NORMAL_MAP => Saves[saveName].DirectoryData.TileNormalMap,

            SaveDataIdentifier.THUMBNAIL_ALEBDO => Saves[saveName].DirectoryData.ThumbnailAlbedo,
            SaveDataIdentifier.THUMBNAIL_HEIGHT_MAP => Saves[saveName].DirectoryData.ThumbnailHeightmap,

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

    #endregion

    #region Image Loading

    public static Dictionary<SaveDataIdentifier, Texture2D> GetThumbnails(string saveName)
    {
        Dictionary<SaveDataIdentifier, Texture2D> images = [];

        for (int i = 0; i < Thumbnails.Length; i++)
        {
            SaveDataIdentifier thumbnail = Thumbnails[i];
            string path = GetDirectoryPath(saveName, thumbnail);

            images[thumbnail] = FileExists(path)
                ? ImageTexture.CreateFromImage(Image.LoadFromFile(path))
                : new PlaceholderTexture2D();
        }

        return images;
    }

    public static Dictionary<SaveDataIdentifier, Texture2D> GetBaseImages(string saveName)
    {
        Dictionary<SaveDataIdentifier, Texture2D> images = [];

        for (int i = 0; i < BaseImages.Length; i++)
        {
            SaveDataIdentifier baseImage = BaseImages[i];
            string path = GetDirectoryPath(saveName, baseImage);

            images[baseImage] = FileExists(path)
                ? ImageTexture.CreateFromImage(Image.LoadFromFile(path))
                : new PlaceholderTexture2D();
        }

        return images;
    }

    #endregion

    #region Shader Loading

    public static RDShaderSource LoadComputeShaderWithIncludes(string shaderPath)
    {
        string shaderSrc = FileAccess.GetFileAsString(shaderPath);
        string[] lines = shaderSrc.Split('\n');

        StringBuilder stringBuilder = new();

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("#[compute]"))
            {
                continue;
            }
            else if (lines[i].TrimStart().Contains("#[include]"))
            {
                string path = lines[i][11..].TrimEnd();
                string includeSrc = FileAccess.GetFileAsString(path);

                stringBuilder.AppendLine("// --- begin include: " + path + " ---");
                stringBuilder.AppendLine(includeSrc);
                stringBuilder.AppendLine("// --- end include: " + path + " ---");
            }
            else
            {
                stringBuilder.AppendLine(lines[i]);
            }
        }

        return new RDShaderSource
        {
            SourceCompute = stringBuilder.ToString(),
            Language = RenderingDevice.ShaderLanguage.Glsl
        };
    }

    public static RDShaderSource LoadGraphicsShaderWithIncludes(string vertexShaderPath, string fragmentShaderPath)
    {
        string shaderSrc = FileAccess.GetFileAsString(vertexShaderPath);
        string[] lines = shaderSrc.Split('\n');

        StringBuilder vertexStringBuilder = new();
        StringBuilder fragmentStringBuilder = new();

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("#[vertex]"))
            {
                continue;
            }
            else if (lines[i].TrimStart().Contains("#[include]"))
            {
                string path = lines[i][11..].TrimEnd();
                string includeSrc = FileAccess.GetFileAsString(path);

                vertexStringBuilder.AppendLine("// --- begin include: " + path + " ---");
                vertexStringBuilder.AppendLine(includeSrc);
                vertexStringBuilder.AppendLine("// --- end include: " + path + " ---");
            }
            else
            {
                vertexStringBuilder.AppendLine(lines[i]);
            }
        }

        shaderSrc = FileAccess.GetFileAsString(fragmentShaderPath);
        lines = shaderSrc.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("#[fragment]"))
            {
                continue;
            }
            else if (lines[i].TrimStart().Contains("#[include]"))
            {
                string path = lines[i][11..].TrimEnd();
                string includeSrc = FileAccess.GetFileAsString(path);

                fragmentStringBuilder.AppendLine("// --- begin include: " + path + " ---");
                fragmentStringBuilder.AppendLine(includeSrc);
                fragmentStringBuilder.AppendLine("// --- end include: " + path + " ---");
            }
            else
            {
                fragmentStringBuilder.AppendLine(lines[i]);
            }
        }

        return new RDShaderSource
        {
            SourceVertex = vertexStringBuilder.ToString(),
            SourceFragment = fragmentStringBuilder.ToString(),
            Language = RenderingDevice.ShaderLanguage.Glsl
        };
    }

    #endregion
}