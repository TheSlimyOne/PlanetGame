using Godot;
using System;

public partial class Test : Godot.MultiMeshInstance3D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// GD.Print(new Basis());
		for (int x = 0; x < 20; x++)
			{

				for (int z = 0; z < 20; z++)
				{
					Transform3D transform = new Transform3D(Basis.Identity, new Vector3(x, 0.0f, -z));
					Multimesh.SetInstanceTransform(z * 20 + x, transform);
					Transform3D newTrans = Multimesh.GetInstanceTransform(z * 20 + x);
					GD.Print(newTrans + " " + "C#");
				}
			}
	}
}
