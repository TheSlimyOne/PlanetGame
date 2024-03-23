using Godot;
using System;
[Tool]
public partial class GravityArea : Area3D
{
	Planet planet;
	public override void _Ready()
	{
		planet = GetNode<Planet>("..");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void OnEnterGravityField(Node3D node)
	{	
		if (node is PlayerController playerController)
		{
			playerController.Focus = planet;
			GD.PrintRich("[color=green] Character Entered Gravity");
		}
		
	}

	public void OnExitGravityField(Node3D node)
	{
		GD.PrintRich("[color=red] Node Exited Gravity");
	}

}
