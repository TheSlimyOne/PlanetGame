using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using PlanetGame.Shaders;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Util;
using static SaveManager;

public partial class MainMenu : MarginContainer
{
	[Export] private OptionButton LoadOptions;
	[Export] private Button LoadSave;
	[Export] private Button AlbedoSave;
	[Export] private Button HeightmapSave;
	[Export] private Button StartNewGame;
	[Export] private Button Quit;

	[Export] private ProgressBar ProgressBar;
	[Export] private Label ProgressLabel;

	[Export] private DemoPlanet DemoPlanet;
	[Export] private ResponsiveFileDialog FileDialog;

	[Export] private LineEdit SaveName;

	private Image NewSaveAlbedo;
	private Image NewSaveHeightmap;

	private string SelectedSave;

	public override void _Ready()
	{
		LoadSave.Disabled = true;
		LoadOptions.Selected = -1;

		DemoPlanet.Planet.Mesh = new BoxMesh() { SubdivideWidth = 16, SubdivideHeight = 16, SubdivideDepth = 16 };
		ShaderMaterial shader = new() { Shader = GD.Load<Shader>(ShaderPaths.DEMO_SHADER_PATH) };
		DemoPlanet.Planet.Mesh.SurfaceSetMaterial(0, shader);
	}

	public override void _EnterTree()
	{
		TileManager.OnTileGeneratedProgress += OnTileProgress;
	}

	public override void _ExitTree()
	{
		TileManager.OnTileGeneratedProgress -= OnTileProgress;
	}

	private void OnTileProgress(int current, string outputText, int maxValue)
	{
		CallDeferred(nameof(UpdateProgressBar), current, outputText, maxValue);
	}

	public void OpenSavesFolder() => OS.ShellShowInFileManager(ProjectSettings.GlobalizePath("user://Saves"));

	public void UpdateProgressBar(int currentCount, string outputText, int maxValue)
	{
		if (ProgressBar == null || ProgressLabel == null)
			return;

		ProgressBar.MinValue = 0;
		ProgressBar.MaxValue = maxValue;
		ProgressBar.Value = currentCount;
		ProgressLabel.Text = outputText;

		if (ProgressBar.Value == ProgressBar.MaxValue)
		{
			ProgressBar.Value = 0;
			ProgressLabel.Text = "";
		}

	}

	public void OnOpenSavesList()
	{
		int previousSelection = LoadOptions.Selected;
		LoadOptions.Clear();
		string[] saveNames = GetSaveNames();
		PopupMenu popupMenu = LoadOptions.GetPopup();
		for (int i = 0; i < saveNames.Length; i++)
		{
			LoadOptions.AddItem(saveNames[i], i);
			popupMenu.SetItemAsRadioCheckable(i, false);
		}
		if (previousSelection < LoadOptions.ItemCount)
		{
			LoadOptions.Selected = previousSelection;
		}
		else
		{
			LoadOptions.Selected = -1;
		}
	}

	public void OnSaveSelection(int index)
	{
		SelectedSave = LoadOptions.GetItemText(index);
		LoadSave.Disabled = false;

		DemoPlanet.SetThumbnails(GetThumbnails(SelectedSave));
	}

	public void OpenFileDialog(string buttonID)
	{
		FileDialog.OpenFileDialog(Callable.From((string path) =>
		{
			Image image = null;
			string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
			if (!FileAccess.FileExists(path) || (extension != ".png" && extension != ".jpg"))
				image = null;
			else
				image = Image.LoadFromFile(path);

			switch (buttonID)
			{
				case "ALBEDO":
					GD.PrintS($"Albedo", path);
					NewSaveAlbedo = image;
					break;
				case "HEIGHT":
					GD.PrintS($"Height", path);
					NewSaveHeightmap = image;
					break;
				default:
					return;
			}
		}));
	}

	public void GenerateTiles()
	{
		string testDir = "user://Tests//Tile Border Test";

		using DirAccess dir = DirAccess.Open(testDir);
		dir.GetFiles().Where(f => f.EndsWith(".png")).ToList().ForEach(f => dir.Remove(f));
		Image image = Image.LoadFromFile("res://Assets/Images/test-image small.png");


		TileManager.GenerateTilesAsync(image, 3, testDir, 0);
	}

	public void OnLoad()
	{
		CurrentSave = SelectedSave;
		GetTree().ChangeSceneToFile("res://Scenes/Planet.tscn");
	}
	public void OnRenerateTiles()
	{
		CurrentSave = SelectedSave;
	}

	public void OnStartNewGame()
	{
		string saveName = SaveName.Text;
		if (NewSaveAlbedo == null || NewSaveHeightmap == null)
			return;

		// string saveName = "TEST BORDER";
		// Image test1 = Image.LoadFromFile("user://Albedo.png");
		// Image test2 = Image.LoadFromFile("user://Heightmap.png");
		// WriteNewSave(saveName, test1, test2, 5, [4, 3, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
		WriteNewSave(saveName, NewSaveAlbedo, NewSaveHeightmap, 5, 0, [4, 4, 3, 3, 2, 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
		GD.Print("Creating Save:", saveName);
		CurrentSave = saveName;
	}

	public void GenerateDebugTiles()
	{
		PackedScene generatorScene = ResourceLoader.Load<PackedScene>("res://Player/UI/DebugTileGenerator.tscn");
		DebugTileGenerator instance = generatorScene.Instantiate<DebugTileGenerator>();
		Image background = Image.CreateEmpty(256, 256, false, Image.Format.Rgbaf);
		int padding = 10;
		background.Fill(Colors.Orange);
		background.FillRect(new Rect2I(padding, padding, 255 - 2 * padding, 255 - 2 * padding), Colors.Black);

		instance.SetBackground(background);
		instance.GenerateDebugTilesAsync(GetTree(), 5);

	}

	public void OnQuit()
	{
		GetTree().Quit();
	}
}
