using System.Threading.Tasks;
using Godot;
using PlanetGame.ComputeShaders;
using static SaveManager;

public partial class MainMenu : MarginContainer
{
	[Export] private OptionButton LoadOptions;
	[Export] private Button LoadSave;
	[Export] private Button AlbedoSave;
	[Export] private Button HeightmapSave;
	[Export] private Button StartNewGame;

	[Export] private DemoPlanet DemoPlanet;
	[Export] private ResponsiveFileDialog FileDialog;

	[Export] private LineEdit SaveName;

	private Image NewSaveAlbedo;
	private Image NewSaveHeightmap;

	private WorldSave NewSave;

	private string SelectedSave;

	public override void _Ready()
	{
		LoadSave.Disabled = true;
		LoadOptions.Selected = -1;

		DemoPlanet.Planet.Mesh = new BoxMesh() { SubdivideWidth = 16, SubdivideHeight = 16, SubdivideDepth = 16 };
		ShaderMaterial shader = new() { Shader = GD.Load<Shader>(ShaderPaths.DEMO_SHADER_PATH) };
		DemoPlanet.Planet.Mesh.SurfaceSetMaterial(0, shader);

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



	public void OnLoad()
	{
		CurrentSave = SelectedSave;
		GetTree().ChangeSceneToFile("res://Scenes/Planet.tscn");
	}

	public void OnStartNewGame()
	{
		// string saveName = SaveName.Text;
		// if (NewSaveAlbedo == null || NewSaveHeightmap == null)
		// 	return;

		string saveName = "Test";
		Image test1 = Image.LoadFromFile("res://Assets/Images/4_no_ice_clouds_mts_16k.jpg");
		Image test2 = Image.LoadFromFile("res://Assets/Images/World_elevation_map.png");
		WriteNewSave(saveName, test1, test2, 16, 256);
	}

}
