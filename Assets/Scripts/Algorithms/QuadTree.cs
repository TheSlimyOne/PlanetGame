using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using static Vector3Utils;
[Tool]
/// <summary>
/// 
/// QuadTree
/// 
/// </summary>
public partial class QuadTree : GodotObject
{
	private QuadTreeNode root;

	private ArrayMesh mesh;

	private float radius;
	private int resolution;

	private const int MaxSubdivisionLevel = 7;
	private int size = 0;
	private Material material;

	private Vector3 normal;
	private int triangleOffset;
	private Vector3 position;
	private Vector3 axisA;
	private Vector3 axisB;

	List<Vector3> vertices = new List<Vector3>();
	List<int> triangles = new List<int>();
	private float distanceScale = 2f;

	private QuadTree() { }

	public QuadTree(SurfaceComponent surfaceComponent)
	{
		mesh = (ArrayMesh)surfaceComponent.Mesh;
		resolution = surfaceComponent.Resolution;
		normal = surfaceComponent.Normal;
		material = surfaceComponent.Material;
		radius = surfaceComponent.Radius;

		position = normal;
		
		axisA = new Vector3(normal.Y, normal.Z, normal.X);
		axisB = normal.Cross(axisA);
		root = new QuadTreeNode(this);
		size++;
	}

	public void UpdateQuadTree(Node3D target)
	{
		vertices.Clear();
		triangles.Clear();
		triangleOffset = 0;

		UpdateTree(target?.GlobalPosition ?? Vector3.Inf, root);
		CallDeferred("ApplyUpdates");
	}

	private void UpdateTree(Vector3 target, QuadTreeNode node)
	{
		
		if (target.DistanceTo(radius * node.position.Normalized()) < node.GetLength() * 2 * radius * distanceScale)
		{
			if (node.subdivisionLevel < MaxSubdivisionLevel)
			{
				node.GenerateChildren();
				UpdateTree(target, node.children[0]);
				UpdateTree(target, node.children[1]);
				UpdateTree(target, node.children[2]);
				UpdateTree(target, node.children[3]);
			}
			else
			{
				(Vector3[], int[]) meshData = node.GenerateMeshChunk(resolution, radius, triangleOffset);
				triangleOffset += meshData.Item1.Length;
				vertices.AddRange(meshData.Item1);
				triangles.AddRange(meshData.Item2);
			}
		}
		else
		{
			(Vector3[], int[]) meshData = node.GenerateMeshChunk(resolution, radius, triangleOffset);
			triangleOffset += meshData.Item1.Length;
			vertices.AddRange(meshData.Item1);
			triangles.AddRange(meshData.Item2);

		}
	}

	private void ApplyUpdates()
	{
		mesh.ClearSurfaces();
		Godot.Collections.Array arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = triangles.ToArray();
		arrays[(int)Mesh.ArrayType.Normal] = vertices.ToArray();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.SurfaceSetMaterial(0, material);
	}

	public override string ToString()
	{
		return $"{{\n{root}\n}}";
	}

	/// <summary>
	/// 
	/// QuadTreeNode
	/// 
	/// </summary>
	protected partial class QuadTreeNode : GodotObject
	{
		internal QuadTree quadTree;
		internal QuadTreeNode parent;
		internal QuadTreeNode[] children = new QuadTreeNode[4];
		internal Vector3 position;
		internal Coordinate coordinate;
		internal bool hasChildren;
		internal Vector3 axisA;
		internal Vector3 axisB;
		internal int subdivisionLevel;

		internal enum Coordinate
		{
			Root,
			NorthWest,
			NorthEast,
			SouthWest,
			SouthEast
		}

		internal QuadTreeNode() { }

		internal QuadTreeNode(QuadTree quadTree)
		{
			this.quadTree = quadTree;
			parent = null;
			position = quadTree.position;
			coordinate = Coordinate.Root;
			axisA = quadTree.axisA;
			axisB = quadTree.axisB;
			hasChildren = false;
			subdivisionLevel = 0;
		}

		internal QuadTreeNode(QuadTree quadTree, QuadTreeNode parent, Vector3 position, Vector3 axisA, Vector3 axisB, Coordinate coordinate, int subdivisionLevel)
		{
			this.quadTree = quadTree;
			this.parent = parent;
			this.position = position;
			this.coordinate = coordinate;
			this.axisA = axisA;
			this.axisB = axisB;
			this.subdivisionLevel = subdivisionLevel;
			hasChildren = false;
		}

		internal void GenerateChildren()
		{
			Vector3[] coordinates = GenerateCornerCoordinates(0.5f);

			children[0] = new QuadTreeNode(quadTree, this, coordinates[0], axisA / 2, axisB / 2, Coordinate.NorthWest, subdivisionLevel + 1);
			children[1] = new QuadTreeNode(quadTree, this, coordinates[1], axisA / 2, axisB / 2, Coordinate.NorthEast, subdivisionLevel + 1);
			children[2] = new QuadTreeNode(quadTree, this, coordinates[2], axisA / 2, axisB / 2, Coordinate.SouthWest, subdivisionLevel + 1);
			children[3] = new QuadTreeNode(quadTree, this, coordinates[3], axisA / 2, axisB / 2, Coordinate.SouthEast, subdivisionLevel + 1);

			hasChildren = true;
		}

		internal float GetLength()
		{
			return 1f / (1 << subdivisionLevel);
		}

		internal List<QuadTreeNode> GetLeafNodes()
		{
			List<QuadTreeNode> leafNodes = new List<QuadTreeNode>();
			if (hasChildren)
				foreach (QuadTreeNode child in children)
					leafNodes.AddRange(child?.GetLeafNodes());
			else
				leafNodes.Add(this);

			return leafNodes;
		}

		internal Vector3[] GenerateCornerCoordinates(float scale)
		{

			Vector3[] coordinates = new Vector3[4];
			// Direction is relative to axisA and axisB
			coordinates[0] = position + scale * (-axisA + axisB);
			coordinates[1] = position + scale * (axisA + axisB);
			coordinates[2] = position + scale * (-axisA - axisB);
			coordinates[3] = position + scale * (axisA - axisB);

			return coordinates;
		}

		internal (Vector3[], int[]) GenerateMeshChunk(int resolution, float radius, int triangleOffset)
		{
			Vector3[] vertices = new Vector3[resolution * resolution];
			int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

			int triIndex = 0;
			for (int x = 0; x < resolution; x++)
			{
				for (int y = 0; y < resolution; y++)
				{
					int vertexIndex = x + y * resolution;

					Vector2 percentage = new Vector2(x, y) / (resolution - 1);
					Vector3 point = position + axisA * (2 * percentage.X - 1) + axisB * (2 * percentage.Y - 1);
					point = PointOnCubeToPointOnSphere(point);

					point *= radius;
					vertices[vertexIndex] = point;

					// Calculates the triangles
					if (x != resolution - 1 && y != resolution - 1)
					{
						triangles[triIndex++] = triangleOffset + vertexIndex;
						triangles[triIndex++] = triangleOffset + vertexIndex + resolution;
						triangles[triIndex++] = triangleOffset + vertexIndex + 1;
						triangles[triIndex++] = triangleOffset + vertexIndex + resolution;
						triangles[triIndex++] = triangleOffset + vertexIndex + resolution + 1;
						triangles[triIndex++] = triangleOffset + vertexIndex + 1;
					}
				}
			}
			return (vertices, triangles);
		}

		internal static Vector3 PointOnCubeToPointOnSphere(Vector3 point)
		{
			float x2 = point.X * point.X;
			float y2 = point.Y * point.Y;
			float z2 = point.Z * point.Z;

			float x = point.X * Mathf.Sqrt(1 - (y2 + z2) / 2 + (y2 * z2) / 3);
			float y = point.Y * Mathf.Sqrt(1 - (z2 + x2) / 2 + (z2 * x2) / 3);
			float z = point.Z * Mathf.Sqrt(1 - (x2 + y2) / 2 + (x2 * y2) / 3);

			return new Vector3(x, y, z);

		}

		public override string ToString()
		{
			string s = "";
			s += $"\n\"{coordinate}\": {{";
			s += $"\n\"position\": [{position.X}, {position.Y}, {position.Z}],";
			s += $"\n\"length\": {GetLength()},";
			s += $"\n{(children[0] != null ? children[0] : $"\"{Coordinate.NorthWest}\": null")},";
			s += $"\n{(children[1] != null ? children[1] : $"\"{Coordinate.NorthEast}\": null")},";
			s += $"\n{(children[2] != null ? children[2] : $"\"{Coordinate.SouthWest}\": null")},";
			s += $"\n{(children[3] != null ? children[3] : $"\"{Coordinate.SouthEast}\": null")}";
			s += $"\n}}";
			return s;
		}
	}
}
