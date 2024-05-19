using Godot;
using System;


public partial class Spawner : MultiMeshInstance3D
{
	[Export] ShaderMaterial material;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		int size = 1;
		ArrayMesh mesh = new ArrayMesh();
        Godot.Collections.Array arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[]{ new Vector3(-1, 0, -1) * size, new Vector3(-1, 0, 1) *size, new Vector3(1, 0, 1) * size, new Vector3(1, 0, -1) * size};
        arrays[(int)Mesh.ArrayType.Index] = new int[]{ 0, 2, 1, 0, 3, 2};
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.SurfaceSetMaterial(0, material);

		Multimesh = new MultiMesh()
        {
            Mesh = mesh,
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseCustomData = true,
            InstanceCount = 100,
        };
		ExtraCullMargin = 100;

		// MeshInstance3D meshInstance3D = new MeshInstance3D();
		// meshInstance3D.Mesh = mesh;
		// AddChild(meshInstance3D);
	}
	float scale = 1;
	int nodeIndex = 0;
	public override void _Input(InputEvent @event)
    {
		
		
        if (@event.IsActionPressed("step"))
        {
			
			GD.Print( 2 * nodeIndex * Vector3.Right);
			Transform3D transform = new Transform3D(Basis.Identity, (3 * scale) * Vector3.Right);
            Multimesh.SetInstanceTransform(nodeIndex, transform);
			Multimesh.SetInstanceCustomData(nodeIndex, new Color(scale,nodeIndex,0,0));
			scale *= 0.5f;
			nodeIndex++;
        }
    }
}
