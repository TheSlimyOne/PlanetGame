using Godot;

public partial class Test3 : Control
{

	[Export] TextureRect textureRect;
	Image image;

    public override void _Ready()
    {
        image = textureRect.Texture.GetImage();
		textureRect.Texture = ImageTexture.CreateFromImage(image);
    }

    public override void _Input(InputEvent @event)
    {
		if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
            {
				
                Vector2 localClickPosition = GetWindow().GetMousePosition().Round();
				localClickPosition /= GetWindow().Size;
				Vector2I pixelCoords = (Vector2I)(localClickPosition * image.GetSize());


                GD.Print($"Image clicked at: {pixelCoords}");
				Color elevation = image.GetPixelv(pixelCoords);
				image.SetPixelv(pixelCoords, Colors.Red);
				// GD.Print(elevation);

				((ImageTexture)textureRect.Texture).Update(image);
            }
        }
    }


}
