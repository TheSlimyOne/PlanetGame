using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using PlanetGame.Data;
using PlanetGame.Util;

public class TileFile : IDisposable
{
    public const uint MAGIC = 0x5649544C;

    public readonly string FilePath;
    private FileAccess _file;

    public uint Magic { get; private set; }
    public uint Version { get; private set; }
    public Image.Format Format { get; private set; }
    public uint TileCount { get; private set; }
    public uint TileDataLength { get; private set; }
    public uint TileSize { get; private set; }

    private ulong _headerSize;
    private ulong _tileSegmentSize;

    public bool IsProcessingTiles { get; private set; } = false;

    public TileFile(string filePath)
    {
        FilePath = filePath;
        if (!FileAccess.FileExists(FilePath))
            throw new InvalidOperationException($"Tile file doesn't exists: {FilePath}");
        
        _file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);

        ReadHeader();
    }

    public TileFile(Vector2I imageSize, Image.Format format, string destination)
    {
        FilePath = destination;
        if (FileAccess.FileExists(FilePath))
            throw new InvalidOperationException($"Tile file already exists: {FilePath}");
        
        _file = FileAccess.Open(FilePath, FileAccess.ModeFlags.WriteRead);

        Magic = MAGIC;
        Version = SaveManager.VERSION;
        Format = format;
        TileCount = GetTileCount(imageSize);
        TileSize = Tile.TILE_SIZE;
        TileDataLength = Tile.TILE_SIZE * Tile.TILE_SIZE * (uint)FormatConverter.GetBytes(format);

        if (!WriteHeader())
        {
            throw new System.IO.IOException($"Failed to write header to file: {FilePath}");
        }

        _headerSize = _file.GetPosition();
        _tileSegmentSize = sizeof(uint) + TileDataLength;
    }

    private void ReadHeader()
    {
        Magic = _file.Get32();
        Version = _file.Get32();
        Format = (Image.Format)_file.Get32();
        TileCount = _file.Get32();
        TileDataLength = _file.Get32();
        TileSize = _file.Get32();

        if (Magic != MAGIC)
            throw new InvalidOperationException("Invalid tile file.");

        _headerSize = _file.GetPosition();
        _tileSegmentSize = sizeof(uint) + TileDataLength;
    }

    private bool WriteHeader()
    {
        bool results = true;
        results &= _file.Store32(Magic);
        results &= _file.Store32(Version);
        results &= _file.Store32((uint)Format);
        results &= _file.Store32(TileCount);
        results &= _file.Store32(TileDataLength);
        results &= _file.Store32(TileSize);

        return results;
    }

    public byte[] GetTileData(Tile tile)
    {
        return GetTileData(tile.GetTileIndex(TileCount));
    }

    public Tile GetTile(uint tileIndex)
    {
        SeekTile(tileIndex);
        return new Tile(_file.Get32());
    }

    public byte[] GetTileData(uint tileIndex)
    {
        SeekTile(tileIndex);

        _file.Get32();

        return _file.GetBuffer(TileDataLength);
    }

    public Image GetTileImage(Tile tile)
    {
        return GetTileImage(tile.GetTileIndex(TileCount));
    }

    public Image GetTileImage(uint tileIndex)
    {
        byte[] data = GetTileData(tileIndex);

        return Image.CreateFromData(
            (int)TileSize,
            (int)TileSize,
            false,
            Format,
            data
        );
    }

    public Tile.TileID GetTileId(uint tileIndex)
    {
        SeekTile(tileIndex);

        return new Tile.TileID(_file.Get32());
    }

    public void WriteTile(Tile tile, Image image)
    {
        byte[] data = image.GetData();

        if (data.Length != TileDataLength)
            throw new InvalidOperationException($"Expected {TileDataLength} bytes, received {data.Length}.");

        _file.Store32(tile.Value);
        _file.StoreBuffer(data);
    }

    private void SeekTile(uint tileIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(tileIndex, TileCount);

        _file.Seek(_headerSize + tileIndex * _tileSegmentSize);
    }

    public void Dispose()
    {
        _file?.Dispose();
        _file = null;

        GC.SuppressFinalize(this);
    }

    public static uint GetTileCount(Vector2I baseImageSize)
    {
        int maxMip = Utilities.Log2(baseImageSize.X) - Utilities.Log2(Tile.TILE_SIZE);
        return 2 * (uint)Mathf.Pow(4, maxMip) - 2;
    }

    public event Action<int, string, int> OnTileGeneratedProgress;

    public struct TileGenerationParams
    {
        public int NormalId { get; set; }
        public int MipIndex { get; set; }
        public Vector2 TileCoordinate { get; set; }
        public Image Source { get; set; }
        public int TilesPerSide { get; set; }
        public int TileSize { get; set; }
        public int Padding { get; set; }

        public override readonly string ToString()
        {
            return $"NormalId: {NormalId}, MipIndex: {MipIndex}, TileCoordinate: {TileCoordinate}, TilesPerSide: {TilesPerSide}, TileSize: {TileSize}, Padding: {Padding}";
        }
    }

    public async Task CreateTiles(Image sourceImage)
    {
        IsProcessingTiles = true;
        List<(Tile Tile, TileGenerationParams Parameters)> tiles = PrepareTiles(sourceImage);
        await ProcessTiles(tiles);
        IsProcessingTiles = false;
    }

    private List<(Tile Tile, TileGenerationParams Parameters)> PrepareTiles(Image sourceImage)
    {
        Image[] mipmaps = GetMipmapSections(sourceImage);

        List<(Tile Tile, TileGenerationParams Parameters)> tiles = [];

        for (int normalId = 0; normalId < 6; normalId++)
        {
            for (int mipIndex = mipmaps.Length - 1; mipIndex >= 0; mipIndex--)
            {
                int tilesPerSide = 1 << (mipmaps.Length - 1 - mipIndex);
                Image sourceMipMap = mipmaps[mipIndex];

                int startingTileEncoding = tilesPerSide * tilesPerSide;
                int endingTileEncoding = startingTileEncoding * 2;

                for (int tileEncoding = startingTileEncoding; tileEncoding < endingTileEncoding; tileEncoding++)
                {
                    Tile tile = new((uint)normalId, (uint)tileEncoding);

                    TileGenerationParams parameters = new()
                    {
                        MipIndex = mipIndex,
                        NormalId = normalId,
                        TileCoordinate = tile.GetTileCoordinate(),
                        Source = sourceMipMap,
                        TilesPerSide = tilesPerSide,
                        TileSize = sourceMipMap.GetSize().Y / tilesPerSide,
                        Padding = 0,
                    };

                    tiles.Add((tile, parameters));
                }
            }
        }

        return tiles;
    }

    private async Task ProcessTiles(List<(Tile Tile, TileGenerationParams Parameters)> tiles)
    {
        int processedTiles = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();
        GD.Print("Starting to process");

        const int BATCH_SIZE = 64;

        for (int i = 0; i < tiles.Count; i += BATCH_SIZE)
        {
            var batch = tiles.Skip(i).Take(BATCH_SIZE).ToArray();

            Task<(Tile Tile, Image Image, string OutputText)>[] tasks = [.. batch
                .Select(entry => Task.Run(() =>
                {
                    Image tileImage = GenerateTileImage(entry.Parameters);

                    string outputText = $"Processing Normal: {entry.Parameters.NormalId} at Mip: {entry.Parameters.MipIndex} for tile coords: {entry.Parameters.TileCoordinate}";

                    return (entry.Tile, tileImage, outputText);
                }))];

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                byte[] imageData = result.Image.GetData();

                WriteTile(result.Tile, result.Image);

                int current = ++processedTiles;
                OnTileGeneratedProgress?.Invoke(current, result.OutputText, (int)TileCount);
            }
        }

        stopwatch.Stop();
        GD.Print($"Done in: {stopwatch.Elapsed}");
    }

    public Image GenerateTileImage(TileGenerationParams parameters)
    {
        int paddedSize = parameters.TileSize + 2 * parameters.Padding;
        Vector2 tileCoordinate = parameters.TileCoordinate;

        Image image = Image.CreateEmpty(paddedSize, paddedSize, false, parameters.Source.GetFormat());

        for (int y = -parameters.Padding; y < parameters.TileSize + parameters.Padding; y++)
        {
            for (int x = -parameters.Padding; x < parameters.TileSize + parameters.Padding; x++)
            {
                Vector2 coordinates = new(x, y);
                Vector2 cubePixel = coordinates + (parameters.TileSize - 1) * tileCoordinate;
                Vector2 cubeUV = cubePixel / ((parameters.TileSize - 1) * parameters.TilesPerSide);

                Vector3 cubePoint = VectorUtils.UVToPointOnCube(parameters.NormalId, cubeUV);
                Vector3 spherePoint = VectorUtils.PointOnCubeToPointOnSphere(cubePoint);
                Vector2 sphereUV = VectorUtils.PointOnSphereToUV(spherePoint);

                Color pixel = Sampler.SampleBilinear(parameters.Source, sphereUV.X, sphereUV.Y);
                image.SetPixel(x + parameters.Padding, y + parameters.Padding, pixel);
            }
        }

        image.Convert(Format);
        return image;
    }

    private static Image[] GetMipmapSections(Image image)
    {
        bool hadMipmaps = image.HasMipmaps();
        if (!hadMipmaps)
            image.GenerateMipmaps();

        int bytesPerPixel = FormatConverter.GetBytes(image.GetFormat());
        List<Image> mipmaps = [];

        for (int i = 0; i < image.GetMipmapCount(); i++)
        {
            Vector2I size = image.GetSize() / (1 << i);
            int mipOffset = (int)image.GetMipmapOffset(i);
            byte[] buffer = new byte[bytesPerPixel * size.X * size.Y];

            Array.Copy(image.GetData(), mipOffset, buffer, 0, buffer.Length);

            Image mipmap = Image.CreateFromData(size.X, size.Y, false, image.GetFormat(), buffer);
            mipmaps.Add(mipmap);

            if (size.Y <= Tile.TILE_SIZE)
                break;
        }

        if (!hadMipmaps)
            image.ClearMipmaps();

        return [.. mipmaps];
    }
}