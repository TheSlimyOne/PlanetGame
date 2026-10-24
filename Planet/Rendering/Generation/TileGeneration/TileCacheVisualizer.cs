using System;
using Godot;
using PlanetGame.Data;
using PlanetGame.Util;

namespace PlanetGame.Planet.Rendering.Generation.TileGeneration
{
	public partial class TileCacheVisualizer : MarginContainer
	{
		private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;

		private OptionButton SaveOptions => GetNode<OptionButton>("%SaveOptions");
		private OptionButton TileTypeOption => GetNode<OptionButton>("%TileTypeOption");
		private OptionButton NormalIdOption => GetNode<OptionButton>("%NormalIdOption");
		private OptionButton MipOption => GetNode<OptionButton>("%MipOption");

		private GridContainer DisplayTextureContainer => GetNode<GridContainer>("%DisplayTextureContainer");
		private Label EncodingLabel => GetNode<Label>("%EncodingLabel");

		private uint NormalId => (uint)NormalIdOption.Selected;
		private uint MipIndex => (uint)MipOption.Selected;

		private SaveManager.SaveDataIdentifier TileType =>
			(SaveManager.SaveDataIdentifier)(int)TileTypeOption.GetSelectedMetadata();

		private TileFile _file;
		private string _selectedSave;

		public override void _Ready()
		{
			SaveOptions.Selected = -1;

			NormalIdOption.ItemSelected += OnNormalIdSelected;
			MipOption.ItemSelected += OnMipSelected;
			TileTypeOption.ItemSelected += OnTileTypeSelected;

			PopulateNormalIdOptions();
			PopulateTileTypeOptions();
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

			string[] saveNames = SaveManager.GetSaveNames();

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
			SaveManager.CurrentSave = _selectedSave;

			LoadFile(SaveManager.GetDirectoryPath(_selectedSave, TileType));
		}

		private void LoadFile(string filePath)
		{
			_file?.Dispose();
			_file = new(filePath);

			GD.PrintS(
				"Visualizer file:",
				filePath,
				"FileLength:",
				_file.FileLength,
				"TileCount:",
				_file.TileCount,
				"TileDataLength:",
				_file.TileDataLength,
				"SegmentSize:",
				_file.TileSegmentSize
			);

			PopulateMipOptions();

			if (_file.TileCount == 0)
			{
				ClearGrid();
				return;
			}

			DisplayMip();
		}

		private void PopulateTileTypeOptions()
		{
			int previousSelection = TileTypeOption.Selected;

			TileTypeOption.Clear();

			foreach (SaveManager.SaveDataIdentifier identifier in SaveManager.Tiles)
			{
				TileTypeOption.AddItem(identifier.ToString());
				TileTypeOption.SetItemMetadata(TileTypeOption.ItemCount - 1, (int)identifier);
			}

			if (TileTypeOption.ItemCount > 0)
				TileTypeOption.Selected = Mathf.Clamp(previousSelection, 0, TileTypeOption.ItemCount - 1);
		}

		private void PopulateNormalIdOptions()
		{
			int previousSelection = NormalIdOption.Selected;

			NormalIdOption.Clear();

			for (int normalId = 0; normalId < 6; normalId++)
				NormalIdOption.AddItem(normalId.ToString());

			NormalIdOption.Selected = Mathf.Clamp(previousSelection, 0, NormalIdOption.ItemCount - 1);
		}

		private void PopulateMipOptions()
		{
			int previousSelection = MipOption.Selected;

			MipOption.Clear();

			if (_file == null || _file.TileCount == 0)
				return;

			int mipCount = (int)VirtualTextureData.TotalMipLayersPerFace;

			for (int mipIndex = 0; mipIndex < mipCount; mipIndex++)
				MipOption.AddItem(mipIndex.ToString());

			if (MipOption.ItemCount > 0)
				MipOption.Selected = Mathf.Clamp(previousSelection, 0, MipOption.ItemCount - 1);
		}

		private void OnTileTypeSelected(long index)
		{
			if (_selectedSave == null)
				return;

			LoadFile(SaveManager.GetDirectoryPath(_selectedSave, TileType));
		}

		private void OnNormalIdSelected(long index)
		{
			if (_file != null)
				DisplayMip();
		}

		private void OnMipSelected(long index)
		{
			if (_file != null)
				DisplayMip();
		}

		private void DisplayMip()
		{
			if (_file == null || _file.TileCount == 0)
				return;

			if (NormalIdOption.Selected < 0 || MipOption.Selected < 0)
				return;

			ClearGrid();

			int mipCount = (int)VirtualTextureData.TotalMipLayersPerFace;
			int tilesPerSide = 1 << (int)MipIndex;

			DisplayTextureContainer.Columns = tilesPerSide;

			for (uint y = 0; y < tilesPerSide; y++)
			{
				for (uint x = 0; x < tilesPerSide; x++)
				{
					uint encoding = Tile.GetTileEncoding(MipIndex, x, y);

					Tile tile = new(NormalId, encoding);
					Image image = _file.GetTileImage(tile);

					float availableSize = Mathf.Min(DisplayTextureContainer.Size.X, DisplayTextureContainer.Size.Y);
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

					uint tileIndex = Tile.GetTileIndex(tile.MipIndex, tile.NormalId, x, y, _file.TileCount);
					uint tileIndex2 = tile.GetTileIndex(_file.TileCount);
					string formattedEncoding = Convert.ToString(tile.Encoding, 2).PadLeft(2 * mipCount - 1, '0');
					string base4 = Utilities.ToBase(tile.Encoding, 4);

					textureRect.MouseEntered += () =>
					{
						EncodingLabel.Text =
							$"Encoding: {tile.Encoding} {Convert.ToString(tile.Encoding, 2).PadLeft(2 * mipCount - 1, '0')} " +
							$"Index: {tile.GetTileIndex(_file.TileCount)}";
					};

					DisplayTextureContainer.AddChild(textureRect);

					Label label = new()
					{
						Text = $"""
						{tile}
						{Utilities.ToBase(tile.Encoding, 2)}
						{Utilities.ToBase(tile.GetParentTile().Encoding, 2)}
						""",
						HorizontalAlignment = HorizontalAlignment.Left,
						VerticalAlignment = VerticalAlignment.Center,

					};

					StyleBoxFlat background = new()
					{
						BgColor = new Color(0, 0, 0, 0.5f)
					};

					label.AddThemeStyleboxOverride("normal", background);

					CenterContainer centerContainer = new();
					centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
					centerContainer.AddChild(label);
					textureRect.AddChild(centerContainer);
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