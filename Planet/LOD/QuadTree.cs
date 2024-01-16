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
public partial class QuadTree
{
	private QuadTreeNode root { get; }

	private Vector3 _origin;
	private Vector3 _normal;
	private float _radius;

	private Vector3 _axisA;
	private Vector3 _axisB;
	private readonly List<QuadTreeNode> _nodeBuffer = new List<QuadTreeNode>();


	private QuadTree() {}

	public QuadTree(Vector3 origin, float radius, Vector3 normal)
	{
		_normal = normal;
		_radius = radius;

		_axisA = new Vector3(normal.Y, normal.Z, normal.X);
		_axisB = normal.Cross(_axisA);
		root = new QuadTreeNode(this, null, normal, _axisA, _axisB, QuadTreeNode.Coordinate.Root, 0);
	}

	public void UpdateQuadTree(Vector3 target)
	{
		_nodeBuffer.Clear();
		UpdateTree(target, root);
	}

	public void SetMeshPositions(MultiMeshInstance3D multiMeshInstance)
	{
		if (Engine.IsEditorHint())
		{	
			_nodeBuffer.Add(root);
		}

		multiMeshInstance.Multimesh.InstanceCount = _nodeBuffer.Count;
		for (int i = 0; i < _nodeBuffer.Count; i++)
		{
			QuadTreeNode node = _nodeBuffer[i];
			Transform3D transform = new Transform3D(Basis.Identity, node.cubePosition);
			Color pointData = new Color(0, 0, 0, node.subdivisionLevel);

			multiMeshInstance.Multimesh.SetInstanceCustomData(i, pointData);
			multiMeshInstance.Multimesh.SetInstanceTransform(i, transform);
		}
	}


	public float Normalize(float value, float min, float max)
	{
		return (value - min) / (max - min);
	}

    readonly Dictionary<int, float> detailLOD = new Dictionary<int, float>()
	{
		{0, 100}, 
		{1, 60}, 
		{2, 20},
		{3, 10}, 
		{4, 4}, 
		{5, 1.5f},
		{6, 0.7f}, 
		{7, 0.3f}, 
		{8, 0.1f},
	};
	
	private void UpdateTree(Vector3 target, QuadTreeNode node)
	{
		
		Vector3 nodeSphereLocation = QuadTreeNode.PointOnCubeToPointOnSphere(node.cubePosition) * _radius;
		
		if (target.Normalized().Dot(nodeSphereLocation.Normalized()) <= -0.5f)
		{
			return;
		}

		if (detailLOD[node.subdivisionLevel] > target.DistanceTo(nodeSphereLocation))
		{	
			if (node.subdivisionLevel < detailLOD.Count - 1)
			{
				if (!node.hasChildren)
				{
					node.GenerateChildren();
				}

				foreach (QuadTreeNode child in node.children)
				{
					UpdateTree(target, child);
				}
			}
			else 
			{
				_nodeBuffer.Add(node);
			}
		} 
		else 
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
	public partial class QuadTreeNode
	{
		internal QuadTree quadTree;
		internal QuadTreeNode parent;
		internal QuadTreeNode[] children = new QuadTreeNode[4];
		internal Vector3 cubePosition;
		internal Vector3 spherePosition;
		internal Coordinate coordinate;
		internal bool hasChildren;
		internal Vector3 axisA;
		internal Vector3 axisB;
		internal int subdivisionLevel;

		internal enum Coordinate
		{
			Root = 0,
			NorthWest = 1,
			NorthEast = 2,
			SouthWest = 3,
			SouthEast = 4
		}

		private QuadTreeNode() {}

		internal QuadTreeNode(QuadTree quadTree, QuadTreeNode parent, Vector3 cubePosition, Vector3 axisA, Vector3 axisB, Coordinate coordinate, int subdivisionLevel)
		{
			this.quadTree = quadTree;
			this.parent = parent;
			this.cubePosition = cubePosition;
			this.coordinate = coordinate;
			this.axisA = axisA;
			this.axisB = axisB;
			this.subdivisionLevel = subdivisionLevel;
			spherePosition = PointOnCubeToPointOnSphere(cubePosition);
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
			coordinates[0] = cubePosition + scale * (-axisA + axisB);
			coordinates[1] = cubePosition + scale * (axisA + axisB);
			coordinates[2] = cubePosition + scale * (-axisA - axisB);
			coordinates[3] = cubePosition + scale * (axisA - axisB);

			return coordinates;
		}

		

		public static Vector3 PointOnCubeToPointOnSphere(Vector3 point)
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
			s += $"\n\"position\": [{cubePosition.X}, {cubePosition.Y}, {cubePosition.Z}],";
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
