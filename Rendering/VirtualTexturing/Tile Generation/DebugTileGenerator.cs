using Godot;
using System;
using System.Threading.Tasks;
namespace PlanetGame.Rendering.VirtualTexturing
{
	public partial class DebugTileGenerator : Control
	{
		[Export] private TextureRect _txrBackground;
		[Export] private Label _lblMipIndex;
		[Export] private Label _lblNormalId;
		[Export] private Label _lblTileCoords;
		[Export] public Vector2I Size;
		[Export] public Viewport Viewport;

		public async Task GenerateDebugTilesAsync(SceneTree tree, int totalMips)
		{
			tree.Root.AddChild(this);

			((SubViewport)Viewport).Size = Size;
			for (int mipIndex = totalMips - 1; mipIndex >= 0; mipIndex--)
			{
				int tilesPerSide = (int)Mathf.Pow(2, totalMips - 1 - mipIndex);

				for (int normalId = 0; normalId < 6; normalId++)
				{
					for (int tileIndex = 0; tileIndex < tilesPerSide * tilesPerSide; tileIndex++)
					{
						int tileY = tileIndex / tilesPerSide;
						int tileX = tileIndex % tilesPerSide;
						string label = $"{mipIndex}-{normalId}-{tileX}-{tileY}";

						_lblMipIndex.Text = mipIndex.ToString();
						_lblNormalId.Text = normalId.ToString();
						_lblTileCoords.Text = $"({tileX}, {tileY})";
						Image outputImage = null;
						await ToSignal(RenderingServer.Singleton, "frame_post_draw");

						outputImage = Viewport.GetTexture().GetImage();
						outputImage.SavePng($"user://Tests/Debug Tile Test/{label}.png");
					}
				}
			}
			
			tree.Root.RemoveChild(this);
		}

		public void SetBackground(Image image)
		{
			_txrBackground.Texture = ImageTexture.CreateFromImage(image);
		}
	}
}
