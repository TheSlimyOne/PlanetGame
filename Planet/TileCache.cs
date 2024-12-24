using System;
using Godot;
using Planet;
using Uniform;

public class TileCache
{
    public Image Cache { get; private set; }
    public IndirectionTable IndirectionTable { get; private set; }
    public int ChunkPixelSize { get; private set; }
    public int ImageSize { get; private set; }
    public ChunkedClipmap ChunkedClipmap { get; private set; }

    public TileCache(IndirectionTable indirectionTable, int chunkPixelSize, ChunkedClipmap chunkedClipmap)
    {
        IndirectionTable = indirectionTable;
        ImageSize = IndirectionTable.GridSize * chunkPixelSize;
        ChunkPixelSize = chunkPixelSize;
        ChunkedClipmap = chunkedClipmap;

        Cache = Image.CreateEmpty(ImageSize, ImageSize, false, Image.Format.Rgbaf);
    }

    public void UpdateCache(PlanetController planetController)
    {
        // Vector3[] normals = new Vector3[] { Vector3.Right, Vector3.Up, Vector3.Back, Vector3.Left, Vector3.Down, Vector3.Forward };
        // Transform3D transform = planetController.PlanetData.Rotation;
        // int gridSize = IndirectionTable.GridSize;
        // int capacity = gridSize * gridSize;
        // int mipDepth = IndirectionTable.MipDepth;
        // int cacheIndex = 0;

        // Rect2I tileRect = new(0, 0, ChunkPixelSize, ChunkPixelSize);


        // for (int pageIndex = 0; pageIndex < 6; pageIndex++)
        // {
        //     Vector3 normal = normals[pageIndex];
        //     Vector3 axisA = new(normal.Y, normal.Z, normal.X);
        //     Vector3 axisB = normal.Cross(axisA);

        //     if (normal != Vector3.Back)
        //     {
        //         continue;
        //     }

        //     for (int mipIndex = 0; mipIndex < mipDepth; mipIndex++)
        //     {
        //         int mipCellStride = (int)Mathf.Pow(2, mipIndex);
        //         for (int x = 0; x < gridSize; x += mipCellStride)
        //         {
        //             for (int y = 0; y < gridSize; y += mipCellStride)
        //             {
        //                 Rect2I indirectionRect = new(new Vector2I(x, y), mipCellStride, mipCellStride);
        //                 if (cacheIndex >= capacity)
        //                 {
        //                     IndirectionTable.Table[mipDepth * pageIndex + (mipDepth - mipIndex - 1)].FillRect(indirectionRect, new Color(-1.0f, -1.0f, -1.0f));
        //                     continue;
        //                 } 

        //                 // if (mipIndex > 0)
        //                 // {
        //                 Vector2 percentage = new Vector2(x + 0.5f, y + 0.5f) / IndirectionTable.GridSize;
        //                 Vector3 position = normal + ((percentage.X - 0.5f) * 2 * axisA + (percentage.Y - 0.5f) * 2 * axisB);
        //                 Vector3 worldPosition = planetController.PlanetData.Radius * VectorUtils.PointOnCubeToPointOnSphere(position);
        //                 Vector3 point = transform * worldPosition;

        //                 if (planetController.CameraController.CalculateDistanceToCam(point) > 1000)
        //                 {
        //                     IndirectionTable.Table[mipDepth * pageIndex + (mipDepth - mipIndex - 1)].FillRect(indirectionRect, new Color(-1.0f, -1.0f, -1.0f));
        //                     continue;
        //                 }

        //                 Vector2I cacheDestination = new Vector2I(cacheIndex % gridSize, cacheIndex / gridSize);
                        

        //                 Image tile = ChunkedClipmap.LoadTile($"res://mips/My_Planet/{mipIndex}-{x / mipCellStride}-{y / mipCellStride}.png");
        //                 GD.Print($"res://mips/My_Planet/{mipIndex}-{x / mipCellStride}-{y / mipCellStride}.png");

        //                 Cache.BlitRect(tile, tileRect, cacheDestination * ChunkPixelSize);

        //                 Vector2 cacheDestinationMinUV = (Vector2)cacheDestination / gridSize;

        //                 IndirectionTable.Table[mipDepth * pageIndex + (mipDepth - mipIndex - 1)].FillRect(indirectionRect, new Color(cacheDestinationMinUV.X, cacheDestinationMinUV.Y, 0));

        //                 cacheIndex++;
        //                 // }



        //             }
        //         }

        //     }
        //     // Cache.SavePng("./IMAGE.png");
        // }
    }

    public Texture2D GetTexture()
    {
        ImageTexture image = new ImageTexture();
        image.SetImage(Cache);
        return image;
    }
}