using Godot;
using System;
using System.Collections.Generic;

public partial class Surface : Node3D
{
	private QuadTreeMeshes _quadTreeMeshes;

	private QuadTree[] _quadTrees = new QuadTree[6];
	public static Dictionary<Vector3, int> normalToIndex = new Dictionary<Vector3, int>()
	{
		{Vector3.Up,      0},
		{Vector3.Down,    1},
		{Vector3.Left,    2},
		{Vector3.Right,   3},
		{Vector3.Forward, 4},
		{Vector3.Back,    5},
	};
	private MultiMeshInstance3D[] _multiMeshInstances = new MultiMeshInstance3D[16];

	public void Initialize(float radius, int resolution, ShaderMaterial material, CompressedTexture2D heightMap)
	{

		_quadTreeMeshes = new QuadTreeMeshes(resolution);
		_quadTreeMeshes.Initialize();

		for (int i = 0; i < 16; i++)
		{
			_multiMeshInstances[i] = GetChild<MultiMeshInstance3D>(i);
			_multiMeshInstances[i].Multimesh = _quadTreeMeshes.Meshes[i];
			_multiMeshInstances[i].Multimesh.Mesh.SurfaceSetMaterial(0, material);
			_multiMeshInstances[i].ExtraCullMargin = 2 * radius;
		}

		for (int i = 0; i < 6; i++)
		{
			_quadTrees[i] = ((Planet)GetParent()).QuadTreesContainer.GetChild<QuadTree>(i);
			_quadTrees[i].Initialize((Planet)GetParent());
			// GD.PrintS(_quadTrees[i].AxisA, _quadTrees[i].AxisB, _quadTrees[i].Normal);
		}
	}

	public QuadTree GetQuadTree(Vector3 normal)
	{
		return _quadTrees[normalToIndex[normal]];
	}


	public void UpdateQuadTrees(Vector3 position)
	{

		Dictionary<int, List<QuadTree.QuadTreeNode>> nodeMeshType = new Dictionary<int, List<QuadTree.QuadTreeNode>>
		{
			{0,  new List<QuadTree.QuadTreeNode>()},
			{1,  new List<QuadTree.QuadTreeNode>()},
			{2,  new List<QuadTree.QuadTreeNode>()},
			{3,  new List<QuadTree.QuadTreeNode>()},
			{4,  new List<QuadTree.QuadTreeNode>()},
			{5,  new List<QuadTree.QuadTreeNode>()},
			{6,  new List<QuadTree.QuadTreeNode>()},
			{7,  new List<QuadTree.QuadTreeNode>()},
			{8,  new List<QuadTree.QuadTreeNode>()},
			{9,  new List<QuadTree.QuadTreeNode>()},
			{10, new List<QuadTree.QuadTreeNode>()},
			{11, new List<QuadTree.QuadTreeNode>()},
			{12, new List<QuadTree.QuadTreeNode>()},
			{13, new List<QuadTree.QuadTreeNode>()},
			{14, new List<QuadTree.QuadTreeNode>()},
			{15, new List<QuadTree.QuadTreeNode>()},
		};

		for (int i = 0; i < 6; i++)
		{
			_quadTrees[i]?.UpdateQuadTree(position);
		}
		for (int i = 0; i < 6; i++)
		{
			_quadTrees[i]?.SetVisibleNodes(nodeMeshType);
		}

		int Count = 0;
		for (int i = 0; i < 16; i++)
		{
			_multiMeshInstances[i].Multimesh.InstanceCount = nodeMeshType[i].Count;
			Count += nodeMeshType[i].Count;

			List<QuadTree.QuadTreeNode> nodes = nodeMeshType[i];

			for (int j = 0; j < nodes.Count; j++)
			{
				QuadTree.QuadTreeNode node = nodes[j];


				Transform3D transform = new Transform3D(Basis.Identity, node.cubePosition);

				Color pointData = new Color(0, 0, (int)node.quadTree.NormalDirection, node.subdivisionLevel);
				uint subdivisionLevel = (uint)node.subdivisionLevel;

				int a = (subdivisionLevel & 0b00000001) != 0 ? 1 : 0;
				int b = (subdivisionLevel & 0b00000010) != 0 ? 1 : 0;
				int c = (subdivisionLevel & 0b00000100) != 0 ? 1 : 0;

				// int a = node.cornerType[0] || node.cornerType[3] ? 1 : 0;
				// int b = node.cornerType[1] || node.cornerType[3] ? 1 : 0;
				// int c = node.cornerType[2] || node.cornerType[3] ? 1 : 0;

				// int a = node.isFan ? 1: 0;
				// int b = node.isEdge[0] || node.isEdge[1] || node.isEdge[2] || node.isEdge[3] ? 1 : 0;

				// if (subdivisionLevel > 7) 
				// 	_collision.CreateCollisionChunk(node.subdivisionLevel, node.quadTree.GetNormal());
					

				_multiMeshInstances[i].Multimesh.SetInstanceColor(j, new Color(a, b, c));
				_multiMeshInstances[i].Multimesh.SetInstanceCustomData(j, pointData);
				_multiMeshInstances[i].Multimesh.SetInstanceTransform(j, transform);
			}

		}
		// GD.Print($"Total amount of multimeshIntance: {Count} All with resolution of {_quadTreeMeshes._resolution}");
	}
}
