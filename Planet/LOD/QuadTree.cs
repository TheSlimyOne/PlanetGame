using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

[Tool]
/// <summary>
/// 
/// QuadTree
/// 
/// </summary>
public partial class QuadTree : GodotObject
{
	private QuadTreeNode root;

	public const int MaxSubdivisionLevel = 7;

	private float _radius;
	private int _resolution;
	private Material _material;
	private Vector3 _normal;
	private Vector3 _position;
	private Vector3 _axisA;
	private Vector3 _axisB;
	private static Random ran = new Random(1207);

	private List<QuadTreeNode> _nodeBuffer = new List<QuadTreeNode>();

	private float distanceScale = 2f;

	private QuadTree() {}

	public QuadTree(Vector3 normal, float radius, Material material, int resolution)
	{
		_resolution = resolution;
		_normal = normal;
		_material = material;
		_radius = radius;
		_position = normal;
		
		_axisA = new Vector3(normal.Y, normal.Z, normal.X);
		_axisB = normal.Cross(_axisA);
		root = new QuadTreeNode(this, null, _position, _axisA, _axisB, QuadTreeNode.Coordinate.Root, 0);
	}

	public void UpdateQuadTree(Vector3 target)
	{
		_nodeBuffer.Clear();
		UpdateTree(target, root);
	}

	public void SpawnChildNodes(Node3D spawnPoint)
	{
		if (_nodeBuffer.Count == 0 && Engine.IsEditorHint())
		{
			_nodeBuffer.Add(root);
		}

		foreach (QuadTreeNode node in _nodeBuffer)
		{
			spawnPoint.AddChild(node);
		}
	}


	private QuadTreeNode[] GetLeafNodes()
	{
		List<QuadTreeNode> leafNodes = new List<QuadTreeNode>();
		GetLeafNodes(root, leafNodes);
		return leafNodes.ToArray();
	}

	private void GetLeafNodes(QuadTreeNode node, List<QuadTreeNode> leafNodes)
	{
		if (!node.hasChildren)
		{
			leafNodes.Add(node);
		}
		else
		{
			foreach (QuadTreeNode child in node.children)
			{
				GetLeafNodes(child, leafNodes);
			}

		}
	}

	private void UpdateTree_new(Vector3 target, QuadTreeNode node)
	{
		if (target.DistanceTo(_radius * node.position.Normalized()) < node.GetLength() * 2 * _radius * distanceScale )
		{

			
			if (node.subdivisionLevel < MaxSubdivisionLevel)
			{
				Vector3[] sectors = node.GenerateCornerCoordinates(0.5f);
				foreach (Vector3 sector in sectors)
				{
					if (target.DistanceTo(_radius * sector.Normalized()) < 1f / (1 << (node.subdivisionLevel - 1)) * 2 * _radius * distanceScale )
					{

					}
				}
				
			}
			else 
			{

			}
		}
		else 
		{

		}
	}
	

	private void UpdateTree(Vector3 target, QuadTreeNode node)
	{
		
		if (target.DistanceTo(_radius * node.position.Normalized()) < node.GetLength() * 2 * _radius * distanceScale)
		{
			if (node.subdivisionLevel < MaxSubdivisionLevel)
			{
				if (!node.hasChildren)
				{
					node.GenerateChildren();
				}
				UpdateTree(target, node.children[0]);
				UpdateTree(target, node.children[1]);
				UpdateTree(target, node.children[2]);
				UpdateTree(target, node.children[3]);
				}
			else 
			{
				_nodeBuffer.Add(node);
			}
		}
		else if (target.Dot(node.position) >= -1)
		{
			_nodeBuffer.Add(node);
		}
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
	public partial class QuadTreeNode : MeshInstance3D
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

		private QuadTreeNode() {}

		internal QuadTreeNode(QuadTree quadTree, QuadTreeNode parent, Vector3 position, Vector3 axisA, Vector3 axisB, Coordinate coordinate, int subdivisionLevel)
		{
			this.quadTree = quadTree;
			this.parent = parent;
			this.position = position;
			this.coordinate = coordinate;
			this.axisA = axisA;
			this.axisB = axisB;
			this.subdivisionLevel = subdivisionLevel;
			Mesh = new ArrayMesh();

            (Vector3[] vertices, int[] triangles) = GenerateMeshChunk(quadTree._resolution, quadTree._radius);
			Godot.Collections.Array arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = vertices;
			arrays[(int)Mesh.ArrayType.Index] = triangles;
			arrays[(int)Mesh.ArrayType.Normal] = vertices;
			((ArrayMesh) Mesh).AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

			StandardMaterial3D sm = new StandardMaterial3D();
			
			Vector3 color_pos = (position + Vector3.One)/2;
			color_pos = color_pos/(subdivisionLevel + 1);
			sm.AlbedoColor = new Color(color_pos.X, color_pos.Y, color_pos.Z);
			Mesh.SurfaceSetMaterial(0, sm);
			
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

		internal (Vector3[], int[]) GenerateMeshChunk(int resolution, float radius)
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
						triangles[triIndex++] = vertexIndex;
						triangles[triIndex++] = vertexIndex + resolution;
						triangles[triIndex++] = vertexIndex + 1;
						triangles[triIndex++] = vertexIndex + resolution;
						triangles[triIndex++] = vertexIndex + resolution + 1;
						triangles[triIndex++] = vertexIndex + 1;
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

			float x = point.X * Mathf.Sqrt(1 - (y2 + z2) / 2 + y2 * z2 / 3);
			float y = point.Y * Mathf.Sqrt(1 - (z2 + x2) / 2 + z2 * x2 / 3);
			float z = point.Z * Mathf.Sqrt(1 - (x2 + y2) / 2 + x2 * y2 / 3);

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
