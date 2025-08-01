using Godot;
using PlanetGame.Rendering.VirtualTexturing;
using System;
public class TextureGenerator
{
	// public int SideLength;
	// public int BorderSize { get; private set; } = 0;
	// public int TilePartitionCount { get; private set; } = 16;

	// public int TileSize => SideLength + 2 * BorderSize;
	// public int TotalSubdivisions => (int)Math.Log2(TilePartitionCount) + 1;

	
	// public string SaveRootPath { get; private set; } = "user://Fake Earth Save";



	// public string BaseAlbedoImageName { get; private set; } = "Fake Earth.png";
	// public string BaseHeightmapImageName { get; private set; } = "Fake Earth Save Height.png";

	// public void GenerateAlbedoMap(string BaseAlbedoImageName)
	// {
	// 	Image image = Image.LoadFromFile($"{SaveRootPath}/Base Images/{BaseAlbedoImageName}");
	// 	image.ResizeToPo2(square: true);
	// 	Vector2I baseImageSize = image.GetSize();
	// 	ChunkManager chunkManager = new(baseImageSize, TilePartitionCount, BorderSize);
	// 	// chunkManager.BorderSize = 
	// 	// chunkManager.QueueGenerateChunksFromImage(SaveRootPath, "Albedo", $"Base Images/{BaseAlbedoImageName}", "Tiles/Albedo Tiles", "Cubemap/Albedo");
	// 	// _ = chunkManager.CreateChunks().ContinueWith(_ => chunkManager.CleanupGPUResources());
	// }

	// public void GenerateHeightmap()
	// {
	// 	Image image = Image.LoadFromFile($"{SaveRootPath}/Base Images/{BaseHeightmapImageName}");
	// 	image.ResizeToPo2(square: true);
	// 	Vector2I baseImageSize = image.GetSize();

		// ChunkManager chunkManager = new(baseImageSize, CenterSize, BorderSize);
	// 	// chunkManager.QueueGenerateChunksFromImage(SaveRootPath, "Heightmap", $"Base Images/{BaseHeightmapImageName}", "Tiles/Heightmap Tiles", "Cubemap/Heightmap", Image.Interpolation.Trilinear);
	// 	// _ = chunkManager.CreateChunks().ContinueWith(_ => chunkManager.CleanupGPUResources());
	// }
}
