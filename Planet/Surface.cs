using Godot;
using System;
using System.Collections;
[Tool]
public partial class Surface : MultiMeshInstance3D
{

	[Export] private Vector3 _normal;
	[Export] private bool _disabled = false;
	private QuadTree _quadTree;

    public void InitializeQuadTree(Vector3 origin, float radius, int resolution, ShaderMaterial material)
	{
		if (!_disabled)
		{
            Multimesh = new MultiMesh 
			{ 
				Mesh = InitializeMesh(resolution, radius), 
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				InstanceCount = 0,
				UseCustomData = true
			};			
			
			Multimesh.Mesh.SurfaceSetMaterial(0, material);
			ExtraCullMargin = 2 * radius;
	
            _quadTree = new QuadTree(origin, radius, _normal);
			_quadTree.SetMeshPositions(this);
		}
	}

	internal Mesh InitializeMesh(int resolution, float radius)
	{
		Vector3[] vertices = new Vector3[resolution * resolution];
		Vector3[] normals = new Vector3[resolution * resolution];
		Vector2[] uvs = new Vector2[resolution * resolution];
		int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

		Vector3 axisA = new Vector3(_normal[1], _normal[2], _normal[0]);
		Vector3 axisB = _normal.Cross(axisA);

		int triIndex = 0;
		for (int x = 0; x < resolution; x++)
		{
			for (int y = 0; y < resolution; y++)
			{
				int vertexIndex = x + y * resolution;
				Vector2 percentage = new Vector2(x, y) / (resolution - 1);
				Vector3 point = _normal + axisA * (2 * percentage.X - 1) + axisB * (2 * percentage.Y - 1);

				vertices[vertexIndex] = _normal;
				normals[vertexIndex] = point;
				uvs[vertexIndex] = percentage;

				if (x != resolution - 1 && y != resolution - 1)
				{
					triangles[triIndex++] = vertexIndex;
					triangles[triIndex++] = vertexIndex + resolution;
					triangles[triIndex++] = vertexIndex + 1;
					triangles[triIndex++] = vertexIndex + resolution;
					triangles[triIndex++] = vertexIndex + resolution + 1;
					triangles[triIndex++] = vertexIndex + 1;
				}
			}
		}
		ArrayMesh mesh = new ArrayMesh();
		Godot.Collections.Array arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Index] = triangles;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		
		return mesh;
	}


	public void UpdateQuadTree(Vector3 position)
	{
		if (!_disabled && _quadTree != null && Multimesh != null)
		{
			_quadTree.UpdateQuadTree(position);
			_quadTree.SetMeshPositions(this);
		}

	}
}
