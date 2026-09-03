using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using PlanetGame.Rendering.Surface;
using PlanetGame.Rendering.VirtualTexturing;

public static class SaveManager
{
    #region Save Data

    public class WorldSave
    {
        public WorldSave() { }

        public TessellationData TessellationData { get; set; } = new();
        public VirtualTextureData VirtualTextureData { get; set; } = new();

        public string BaseDirectory { get; set; }

        public string BaseAlbedo { get; set; }
        public string BaseHeightmap { get; set; }

        public string ThumbnailAlbedo { get; set; }
        public string ThumbnailHeightmap { get; set; }

        public string TilesAlbedo { get; set; }
        public string TilesHeightmap { get; set; }
        public string TilesNormalMap { get; set; }

        public Vector3 PlanetPosition { get; set; } = Vector3.Zero;
        public Vector3 PlanetRotation { get; set; } = Vector3.Zero;
        public Vector3 PlanetScale { get; set; } = Vector3.One;

        public Transform3D GetTranslationTransform()
        {
            return new Transform3D(Basis.Identity, PlanetPosition);
        }

        public Transform3D GetRotationTransform()
        {
            return new Transform3D(Basis.FromEuler(PlanetRotation), Vector3.Zero);
        }

        public Transform3D GetScaleTransform()
        {
            return new Transform3D(Basis.Identity.Scaled(PlanetScale), Vector3.Zero);
        }

        public Transform3D[] GetTransforms()
        {
            return
            [
                GetTranslationTransform(),
                GetRotationTransform(),
                GetScaleTransform()
            ];
        }

        public override string ToString()
        {
            return $"""
            TessellationData:
            {TessellationData}

            VirtualTextureData:
            {VirtualTextureData}

            BaseDirectory: {BaseDirectory}
            BaseAlbedo: {BaseAlbedo}
            BaseHeightmap: {BaseHeightmap}

            ThumbnailAlbedo: {ThumbnailAlbedo}
            ThumbnailHeightmap: {ThumbnailHeightmap}

            TilesAlbedo: {TilesAlbedo}
            TilesHeightmap: {TilesHeightmap}
            TilesNormalMap: {TilesNormalMap}

            PlanetPosition: {PlanetPosition}
            PlanetRotation: {PlanetRotation}
            PlanetScale: {PlanetScale}
            """;
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

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        AllowTrailingCommas = true,
        IncludeFields = true
    };

    private static Dictionary<string, WorldSave> Saves = GetSaves();

    public static string CurrentSave { get; set; }

    public static WorldSave CurrentWorldSave => Saves[CurrentSave];


    #endregion

    #region Save Reading And Writing

    private static Dictionary<string, WorldSave> GetSaves()
    {
        if (!FileAccess.FileExists(SAVE_PATH))
            return [];

        using FileAccess fileAccess = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Read);

        string jsonText = fileAccess.GetAsText();

        if (string.IsNullOrWhiteSpace(jsonText))
            return [];

        return JsonSerializer.Deserialize<Dictionary<string, WorldSave>>(jsonText, Options) ?? [];
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

    public static void OverrideSave(string saveName, WorldSave worldSave)
    {
        Saves[saveName] = worldSave;
        WriteSaves();
    }

    public static WorldSave GetSave(string saveName)
    {
        return Saves[saveName];
    }

    public static string[] GetSaveNames()
    {
        return [.. Saves.Keys];
    }

    public static bool SaveNameExist(string saveName)
    {
        if (!Saves.TryGetValue(saveName, out WorldSave save))
            return false;

        return DirectoryExist(save.BaseDirectory);
    }

    #endregion

    #region Save Creation

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

    public static async Task WriteNewSave(string saveName, Image albedo, Image heightmap, int lowResolutionMipCount, int highResolutionMipCount, int[] lodToMipMap)
    {
        Vector2I size = new(16384, 8192);

        albedo.Resize(size.X, size.Y, Image.Interpolation.Bilinear);

        if (albedo.GetSize() != heightmap.GetSize())
            heightmap.Resize(albedo.GetSize().X, albedo.GetSize().Y);

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

            VirtualTextureData = new VirtualTextureData(
                (uint)(albedo.GetHeight() / Mathf.Pow(2, lowResolutionMipCount - 1)),
                (uint)lowResolutionMipCount,
                (uint)highResolutionMipCount,
                lodToMipMap,
                [
                    "4_0_0_0",
                    "4_1_0_0",
                    "4_2_0_0",
                    "4_3_0_0",
                    "4_4_0_0",
                    "4_5_0_0",
                ]
            ),

            TessellationData = new TessellationData(
                radius: 100,
                resolution: 5,
                heightScale: 0.025f,
                subFactor: 4,
                maximumLod: 12,
                minimumLod: 0,
                maximumKeys: 40000,
                startingLod: 2,
                cullingDepth: 1,
                cullingMargin: new Vector4(0.09f, 0.09f, 0.3f, 15)
            )
        };

        albedo.SavePng(paths.BaseAlbedo);
        heightmap.SavePng(paths.BaseHeightmap);

        GenerateThumbnail(albedo).SavePng(paths.ThumbnailAlbedo);
        GenerateThumbnail(heightmap).SavePng(paths.ThumbnailHeightmap);

        await GenerateTiles(worldSave, albedo, heightmap);

        OverrideSave(saveName, worldSave);
    }

    #endregion

    #region Transform Saving

    public static void StoreCurrentTransform(string saveName, Transform3D translation, Transform3D rotation, Transform3D scale)
    {
        WorldSave save = GetSave(saveName);

        save.PlanetPosition = translation.Origin;
        save.PlanetRotation = rotation.Basis.GetEuler();
        save.PlanetScale = scale.Basis.Scale;
    }

    #endregion

    #region Tile Generation

    public static async Task GenerateTiles(WorldSave save, Image albedo, Image heightmap)
    {
        GD.Print("Generating");

        int mipCount = (int)(
            save.VirtualTextureData.LowResolutionMipCount +
            save.VirtualTextureData.HighResolutionMipCount
        );

        GD.Print("Generating Albedo map");

        await TileManager.GenerateTilesAsync(
            albedo,
            mipCount - 1,
            save.TilesAlbedo,
            0
        );

        GD.Print("Generating Heightmap");

        await TileManager.GenerateTilesAsync(
            heightmap,
            mipCount - 1,
            save.TilesHeightmap,
            0
        );
    }

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
        return Saves[saveName].BaseDirectory;
    }

    public static string GetDirectoryPath(string saveName, SaveDataIdentifier directory)
    {
        return directory switch
        {
            SaveDataIdentifier.ROOT_SAVE_DIRECTORY => Saves[saveName].BaseDirectory,

            SaveDataIdentifier.BASE_ALBEDO => Saves[saveName].BaseAlbedo,
            SaveDataIdentifier.BASE_HEIGHT_MAP => Saves[saveName].BaseHeightmap,

            SaveDataIdentifier.TILE_ALBEDO => Saves[saveName].TilesAlbedo,
            SaveDataIdentifier.TILE_HEIGHT_MAP => Saves[saveName].TilesHeightmap,
            SaveDataIdentifier.TILE_NORMAL_MAP => Saves[saveName].TilesNormalMap,

            SaveDataIdentifier.THUMBNAIL_ALEBDO => Saves[saveName].ThumbnailAlbedo,
            SaveDataIdentifier.THUMBNAIL_HEIGHT_MAP => Saves[saveName].ThumbnailHeightmap,

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

    public static Image GetTile(string saveName, SaveDataIdentifier tileIdentifier, string fileName)
    {
        string directory = GetDirectoryPath(saveName, tileIdentifier);
        string path = $"{directory}/{fileName}.png";

        return Image.LoadFromFile(path);
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