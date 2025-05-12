using Godot;

public partial class TestWindow : Window
{
	[Export] HBoxContainer TextureContainer;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void OnCloseRequested()
	{
		QueueFree();
	}

	public void PopulateWithTextures(Image[] images)
	{
		foreach (var image in images)
		{
			ImageTexture imageTexture = ImageTexture.CreateFromImage(image);
            TextureRect textureRect = new()
            {
                Texture = imageTexture
            };
            TextureContainer.AddChild(textureRect);
		}
	}
}
