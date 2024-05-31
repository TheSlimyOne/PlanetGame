using Godot;
using System;
[Tool]
public partial class GravityField : Area3D
{
	[Export] public Planet Planet;
	[Export] public CollisionShape3D Collision;



	public void UpdateRadius(float radius)
	{
		Collision.Shape = new SphereShape3D();
		((SphereShape3D)Collision.Shape).Radius = radius;
	}

    public void OnEnterGravityField(Node3D node)
	{	
		if (node is PlayerControllerBody character)
		{
			character.Gimbal.Focus = Planet;
			GD.PrintRich("[color=green] Character Entered Gravity");
		}
		
	}

	public void OnExitGravityField(Node3D node)
	{
		GD.PrintRich("[color=red] Node Exited Gravity");
	}

}
