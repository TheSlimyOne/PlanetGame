using System;
using Godot;
using PlanetGame.Data;
using static SaveManager;

namespace PlanetGame.Planet.Rendering.Generation.TileGeneration
{
	public partial class TileCacheVisualizer : MarginContainer
	{
		private OptionButton SaveOptions => GetNode<OptionButton>("%SaveOptions");
		private OptionButton NormalIdOption => GetNode<OptionButton>("%NormalIdOption");
		private OptionButton MipOption => GetNode<OptionButton>("%MipOption");

		private GridContainer DisplayTextureContainer => GetNode<GridContainer>("%DisplayTextureContainer");

		private Label EncodingLabel => GetNode<Label>("%EncodingLabel");
		
		private TileFile _file;
		private string _selectedSave;

		private uint _normalId;
		private uint _mipIndex;

		public override void _Ready()
		{
			SaveOptions.Selected = -1;

			NormalIdOption.ItemSelected += OnNormalIdSelected;
			MipOption.ItemSelected += OnMipSelected;

			PopulateNormalIdOptions();
		}

		public override void _ExitTree()
		{
			_file?.Dispose();
		}

		public void OnOpenSavesList()
		{
			string previousSelection =
				SaveOptions.Selected >= 0
					? SaveOptions.GetItemText(SaveOptions.Selected)
					: null;

			SaveOptions.Clear();

			string[] saveNames = GetSaveNames();

			foreach (string saveName in saveNames)
				SaveOptions.AddItem(saveName);

			if (previousSelection == null)
			{
				SaveOptions.Selected = -1;
				return;
			}

			for (int i = 0; i < SaveOptions.ItemCount; i++)
			{
				if (SaveOptions.GetItemText(i) != previousSelection)
					continue;

				SaveOptions.Selected = i;
				return;
			}

			SaveOptions.Selected = -1;
		}

		public void OnSaveSelection(int index)
		{
			if (index < 0 || index >= SaveOptions.ItemCount)
				return;

			_selectedSave = SaveOptions.GetItemText(index);

			LoadFile(
				GetDirectoryPath(
					_selectedSave,
					SaveDataIdentifier.TILE_ALBEDO
				)
			);
		}

		private void LoadFile(string filePath)
		{
			_file?.Dispose();
			_file = new TileFile(filePath);

			PopulateMipOptions();

			if (_file.TileCount == 0)
			{
				ClearGrid();
				return;
			}

			_normalId = 0;
			_mipIndex = 0;

			NormalIdOption.Selected = 0;
			MipOption.Selected = 0;

			DisplayMip();
		}

		private void PopulateNormalIdOptions()
		{
			NormalIdOption.Clear();

			for (int normalId = 0; normalId < 6; normalId++)
				NormalIdOption.AddItem(normalId.ToString());

			NormalIdOption.Selected = 0;
		}

		private void PopulateMipOptions()
		{
			MipOption.Clear();

			if (_file == null || _file.TileCount == 0)
				return;

			Tile lastTile = Tile.GetTileByIndex(_file.TileCount - 1, _file.TileCount);
			int mipCount = lastTile.MipIndex + 1;

			for (int mipIndex = 0; mipIndex < mipCount; mipIndex++)
				MipOption.AddItem(mipIndex.ToString());

			MipOption.Selected = 0;
		}

		private void OnNormalIdSelected(long index)
		{
			if (_file == null)
				return;

			_normalId = (uint)index;

			DisplayMip();
		}

		private void OnMipSelected(long index)
		{
			if (_file == null)
				return;

			_mipIndex = (uint)index;

			DisplayMip();
		}

		private void DisplayMip()
		{
			if (_file == null)
				return;

			ClearGrid();

			int tilesPerSide = 1 << (int)_mipIndex;

			DisplayTextureContainer.Columns = tilesPerSide;

			for (uint y = 0; y < tilesPerSide; y++)
			{
				for (uint x = 0; x < tilesPerSide; x++)
				{
					uint encoding = Tile.TileID.GetTileEncoding(
						_mipIndex,
						x,
						y
					);

					Tile tile = new(_normalId, encoding);
					Image image = _file.GetTileImage(tile);

					float availableSize = Mathf.Min(
						DisplayTextureContainer.Size.X,
						DisplayTextureContainer.Size.Y
					);

					float tileSize = availableSize / tilesPerSide;

					TextureRect textureRect = new()
					{
						Texture = ImageTexture.CreateFromImage(image),
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.Scale,

						SizeFlagsHorizontal = SizeFlags.ExpandFill,
						SizeFlagsVertical = SizeFlags.ExpandFill,
						TooltipText =
							$"Normal Id: {tile.NormalId}\n" +
							$"Mip: {tile.MipIndex}\n" +
							$"Encoding: {tile.Encoding}\n" +
							$"Coordinate: {tile.GetTileCoordinate()}"
					};

					textureRect.MouseEntered += () =>
					{
						EncodingLabel.Text =
							$"Encoding: {tile.Encoding} " +
							Convert.ToString(tile.Encoding, 2)
								.PadLeft(Tile.TileID.ENCODING_BITS, '0');
					};

					DisplayTextureContainer.AddChild(textureRect);
				}
			}
		}

		private void ClearGrid()
		{
			foreach (Node child in DisplayTextureContainer.GetChildren())
			{
				DisplayTextureContainer.RemoveChild(child);
				child.QueueFree();
			}
		}
	}
}