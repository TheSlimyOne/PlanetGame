using Godot;
using System;
[Tool]

public partial class RenderingComponent : Node
{

	RenderingDevice renderingDevice = RenderingServer.CreateLocalRenderingDevice();
	[Export]
	RDShaderFile shaderFile;


	public override void _Ready()
	{

	}

}
