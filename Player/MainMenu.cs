using Godot;
using PlanetGame.Shaders;
// using static SaveManager;

public partial class MainMenu : MarginContainer
{
	public static MainMenu Instance { get; private set; }

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

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public override void _Ready()
	{
		LoadSave.Disabled = true;
		LoadOptions.Selected = -1;

		DemoPlanet.Planet.Mesh = new BoxMesh() { SubdivideWidth = 16, SubdivideHeight = 16, SubdivideDepth = 16 };
		ShaderMaterial shader = new() { Shader = GD.Load<Shader>(ShaderPaths.GD_DEMO_SHADER_PATH) };
		DemoPlanet.Planet.Mesh.SurfaceSetMaterial(0, shader);
	}

	// public override void _EnterTree()
	// {
	// 	TileGenerator.OnTileGeneratedProgress += OnTileProgress;
	// }

	// public override void _ExitTree()
	// {
	// 	TileGenerator.OnTileGeneratedProgress -= OnTileProgress;
	// }

	private void OnTileProgress(int current, string outputText, int maxValue)
	{
		CallDeferred(nameof(UpdateProgressBar), current, outputText, maxValue);
	}

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
		
		// RefreshSaves();
		string[] saveNames = SaveManager.GetSaveNames();
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

	public void OpenSavesFolder()
	{
		OS.ShellOpen(ProjectSettings.GlobalizePath("user://"));
	}

	public void OnSaveSelection(int index)
	{
		if (index < 0 || index >= LoadOptions.ItemCount)
			return;

		SelectedSave = LoadOptions.GetItemText(index);
		LoadSave.Disabled = false;

		DemoPlanet.SetThumbnails(SaveManager.GetThumbnails(SelectedSave));
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
		// string testDir = "user://Tests//Tile Border Test";

		// using DirAccess dir = DirAccess.Open(testDir);
		// dir.GetFiles().Where(f => f.EndsWith(".png")).ToList().ForEach(f => dir.Remove(f));
		// Image image = Image.LoadFromFile("res://Assets/Images/test-image small.png");


		// TileManager.GenerateTilesAsync(image, 3, testDir, 0);
	}

	public void OnLoad()
	{
		SaveManager.CurrentSave = SelectedSave;
		GetTree().ChangeSceneToFile("res://Planet/planet.tscn");
	}
	public void OnRenerateTiles()
	{
		SaveManager.CurrentSave = SelectedSave;
	}

	public void OnStartNewGame()
	{
		string saveName = SaveName.Text;
		if (NewSaveAlbedo == null || NewSaveHeightmap == null)
		{
			saveName = "Earth";
			NewSaveAlbedo = Image.LoadFromFile("user://Albedo.png");
			NewSaveHeightmap = Image.LoadFromFile("user://Heightmap.png");
		}

		// WriteNewSave(saveName, test1, test2, 5, [4, 3, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
		GD.Print("Creating Save:", saveName);
		SaveManager.WriteNewSave(saveName, NewSaveAlbedo, NewSaveHeightmap, 
		[
			0, 1, 2, 3, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5
		], OnTileProgress);
		SaveManager.CurrentSave = saveName;
	}

	public void GenerateDebugTiles()
	{
		// PackedScene generatorScene = ResourceLoader.Load<PackedScene>("res://Player/UI/DebugTileGenerator.tscn");
		// DebugTileGenerator instance = generatorScene.Instantiate<DebugTileGenerator>();
		// Image background = Image.CreateEmpty(256, 256, false, Image.Format.Rgbaf);
		// int padding = 10;
		// background.Fill(Colors.Orange);
		// background.FillRect(new Rect2I(padding, padding, 255 - 2 * padding, 255 - 2 * padding), Colors.Black);

		// instance.SetBackground(background);
		// instance.GenerateDebugTilesAsync(GetTree(), 5);
		Image test1 = Image.LoadFromFile("user://Albedo.png");
	}

	public void OnQuit()
	{
		GetTree().Quit();
	}
}
