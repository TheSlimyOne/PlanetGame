using Dispatcher;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class TestScene : Node
{
	[Export] TextureRect A;
	[Export] TextureRect B;
	[Export] TextureRect C;
	[Export] int size;
	// [Export] int chunkSize;
	[Export] float maxSlope;
	[Export] Image.Interpolation interpolation;

	[ExportGroup("Compute Shader")]
	[Export(PropertyHint.File)] private string _shaderPath;

    // [ExportToolButton("Execute Shader")]
    // public Callable ExecuteShaderButton => Callable.From(ExecuteShader);

    public override void _Ready()
    {
        ExecuteShader();
    }

    public void ExecuteShader()
	{
		// RenderingDevice rd = RenderingServer.GetRenderingDevice();
		// CalculateSlopeMapDispatcher calculateSlope = new(_shaderPath, rd);
		// calculateSlope.MaxSlope = maxSlope;
		// calculateSlope.HeightScale = 1;
		// calculateSlope.InputTexture = A.Texture;
		// calculateSlope.CreateUniforms();
		// calculateSlope.Ready();
		// B.Texture = ImageTexture.CreateFromImage(calculateSlope.GetSlopeMap());
		// calculateSlope.CleanupGPU();
		Image image = A.Texture.GetImage();
		
		Image A1 = Image.CreateEmpty(image.GetWidth() / 2, image.GetHeight(), false, image.GetFormat());
		Image A2 = Image.CreateEmpty(image.GetWidth() / 2, image.GetHeight(), false, image.GetFormat());

		A1.BlitRect(image, new Rect2I(Vector2I.Zero, Vector2I.One * image.GetHeight()), Vector2I.Zero);
		Image B1 = ProcessImage(A1, 4, 2);
		B1.Resize(size, size, interpolation: interpolation);

		B1.SavePng("user://test/BIGA.png");
		B.Texture = ImageTexture.CreateFromImage(B1);

		A2.BlitRect(image, new Rect2I(new Vector2I(image.GetHeight(), 0), new Vector2I(2 * image.GetHeight(), image.GetHeight())), Vector2I.Zero);
		Image B2 = ProcessImage(A2, 4, 2);
		B2.Resize(size, size, interpolation: interpolation);

		B2.SavePng("user://test/BIGB.png");
		C.Texture = ImageTexture.CreateFromImage(B2);
		

		// A2.BlitRect(image, new Rect2I(new Vector2I(image.GetHeight(), 0), Vector2I.One * image.GetHeight()), Vector2I.Zero);
		// A2.Resize(size, size, interpolation: interpolation);
		// C.Texture = ImageTexture.CreateFromImage(A2);

	}

	public Image ProcessImage(Image baseImage, int numChunksPerRow, int padding)
	{
		int chunkSize = baseImage.GetWidth() / numChunksPerRow;
		Image blurredHeightMap = Image.CreateEmpty(baseImage.GetWidth(), baseImage.GetHeight(), false, Image.Format.R8);
		
		if (padding > chunkSize) throw new ArgumentException("padding > chunkSize");

		RenderingDevice rd = RenderingServer.CreateLocalRenderingDevice();
		
		for (int rowIndex = 0; rowIndex < numChunksPerRow; rowIndex++)
		{
			for (int colIndex = 0; colIndex < numChunksPerRow; colIndex++)
			{
				Image chunk = CreateChunk(chunkSize, numChunksPerRow, padding, baseImage, rowIndex, colIndex);
	
				BlurImageDispatcher dispatcher = new(_shaderPath, rd)
				{
					Padding = padding,
					HeightMap = chunk
				};
				dispatcher.CreateUniforms();
				dispatcher.Invoke();

				dispatcher.Submit();
				dispatcher.Sync();
				Image updatedChunk = dispatcher.GetBlurredHeightMap();

				blurredHeightMap.BlitRect(updatedChunk, new Rect2I(0, 0, chunkSize, chunkSize), new Vector2I(colIndex * chunkSize, rowIndex * chunkSize));
				dispatcher.CleanupGPU();
			}

		}

		blurredHeightMap.Convert(Image.Format.L8);
		return blurredHeightMap;
	}
	public Image CreateChunk(int chunkSize, int numChunksPerRow, int padding, Image baseImage, int rowIndex, int colIndex)
	{
		int imageSize = baseImage.GetWidth();
		int paddedSize = chunkSize + 2 * padding;

		Image chunk = Image.CreateEmpty(paddedSize, paddedSize, false, baseImage.GetFormat());

		chunk.BlitRect(
			baseImage,
			new Rect2I(colIndex * chunkSize, rowIndex * chunkSize, chunkSize, chunkSize),
			Vector2I.One * padding
		);

		chunk.BlitRect(
			baseImage,
			new Rect2I(colIndex * chunkSize - padding, rowIndex * chunkSize - padding, padding, chunkSize + padding),
			Vector2I.Zero
		);

		chunk.BlitRect(
			baseImage,
			new Rect2I(colIndex * chunkSize - padding, rowIndex * chunkSize + chunkSize, chunkSize + padding, padding),
			new Vector2I(0, chunkSize + padding)
		);

		chunk.BlitRect(
			baseImage,
			new Rect2I(colIndex * chunkSize + chunkSize, rowIndex * chunkSize, padding, chunkSize + padding),
			new Vector2I(chunkSize + padding, padding)
		);

		chunk.BlitRect(
			baseImage,
			new Rect2I(colIndex * chunkSize, rowIndex * chunkSize - padding, chunkSize + padding, padding),
			new Vector2I(padding, 0)
		);

		if (colIndex == 0)
		{
			chunk.BlitRect(baseImage,
				new Rect2I(imageSize - padding, rowIndex * chunkSize - padding, padding, paddedSize),
				Vector2I.Zero
			);
		}
		if (colIndex == numChunksPerRow - 1)
		{
			chunk.BlitRect(baseImage,
				new Rect2I(0, rowIndex * chunkSize - padding, padding, paddedSize),
				new Vector2I(paddedSize - padding, 0)
			);
		}

		if (rowIndex == 0)
		{
			for (int i = 0; i < padding; i++)
			{
				chunk.BlitRect(chunk,
					new Rect2I(0, padding, paddedSize, 1),
					new Vector2I(0, i)
				);
			}
		}

		if (rowIndex == numChunksPerRow - 1)
		{
			for (int i = 0; i < padding; i++)
			{
				chunk.BlitRect(chunk,
					new Rect2I(0, chunkSize + padding - 1, paddedSize, 1),
					new Vector2I(0, chunkSize + padding + i)
				);
			}
		}

		return chunk;

	}


}
