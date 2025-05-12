using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGame.Util.Orometry;
using PlanetGame.Util;
using Gradient = PlanetGame.Util.Orometry.Gradient;
using Direction = PlanetGame.Util.Orometry.Gradient.GradientDirection;
[Tool]
public partial class test4 : Node3D
{
	[Export] MeshInstance3D Terrain;
	[Export] MultiMeshInstance3D GradientArrows;
	[Export] float ArrowOffset;
	[Export] float ArrowScale = 1;

	[Export] MultiMeshInstance3D Simplexes;
	[Export] float SimplexOffset;
	[Export] float SimplexScale = 1;

	[Export] float HeightMapScale;
	[Export] Texture2D Texture;
	[Export] Material Material;

	[Export] MultiMeshInstance3D Lines;
	[Export] Material LineMaterial;

	[Export] bool Debug;
	[Export] bool FullPath;
	[Export] MeshInstance3D Tracker;

	[Export] float PersistenceThreshold;

	[Export]
	public int Timestep
	{
		get => _timestep;
		set
		{
			if (_timestep != value)
			{
				_timestep = value;
				if (Terrain != null)
					Run();
			}
		}
	}

	private int _timestep;

	[ExportToolButton("Generate")]
	public Callable Execute => Callable.From(Run);

	private Vector2I _size = new();

	public void Run()
	{
		Image image = Texture.GetImage();
		if (image.IsCompressed()) image.Decompress();
		// float[,] elevationData = HeightmapAnalyzer.RefactorHeightMap(image);
		float[,] elevationData = HeightmapAnalyzer.ImageTo2dArray(image);

		HeightmapAnalyzer heightmapAnalyzer = new(elevationData, PersistenceThreshold, Debug, Timestep, Tracker);
		_size = new(elevationData.GetLength(0), elevationData.GetLength(1));
		Image refactoredImage = Image.CreateFromData(_size.X, _size.Y, false, Image.Format.L8, Utilities.ToBytes8(elevationData));


		((ShaderMaterial)Material).SetShaderParameter("image", ImageTexture.CreateFromImage(refactoredImage));
		Terrain.Mesh = heightmapAnalyzer.GetHeightMapMesh(elevationData, Material, HeightMapScale);

		Lines.Multimesh = new()
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseCustomData = true,
			UseColors = true,
			Mesh = new Func<Mesh>(() =>
			{
				ImmediateMesh immediateMesh = new();
				immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, new StandardMaterial3D());
				immediateMesh.SurfaceAddVertex(Vector3.Zero);
				immediateMesh.SurfaceAddVertex(Vector3.Up);
				immediateMesh.SurfaceEnd();
				immediateMesh.SurfaceSetMaterial(0, LineMaterial);
				return immediateMesh;
			}).Invoke()
		};

		DrawMorseSmaleComplex(heightmapAnalyzer);
	}

	private void DrawGradientArrows(HeightmapAnalyzer heightmapAnalyzer)
	{
		var nonCriticalEdges = heightmapAnalyzer.Gradients
			.Where(x => x.Value.IsDirectional())
			.Where(x => x.Key is Point || x.Key is Edge)
			.ToList();

		GradientArrows.Multimesh.InstanceCount = 0;
		GradientArrows.Multimesh.InstanceCount = nonCriticalEdges.Count;
		for (int i = 0; i < nonCriticalEdges.Count; i++)
		{
			Simplex simplex = nonCriticalEdges[i].Key;
			Vector3 position = TransformCentroid(simplex.GetCentroid() * new Vector3(1, 0.5f, 1), HeightMapScale + ArrowOffset);

			Transform3D transform3D = new(Basis.Identity, Vector3.Zero);

			transform3D = transform3D.Scaled(Vector3.One * ArrowScale);

			transform3D = transform3D.Rotated(Vector3.Up, Gradient.ToRotation(heightmapAnalyzer.Gradients[simplex].Direction));
			transform3D = transform3D.Translated(position);

			GradientArrows.Multimesh.SetInstanceTransform(i, transform3D);
			GradientArrows.Multimesh.SetInstanceColor(i, ToColor(heightmapAnalyzer.Gradients[simplex].Direction));
		}
	}

	private void DrawManifold(HeightmapAnalyzer heightmapAnalyzer)
	{
		var paths = heightmapAnalyzer.Manifold.GetPaths();

		Lines.Multimesh.InstanceCount = FullPath ? paths.Sum(path => path.Count - 1) : paths.Count * 2;
		Simplexes.Multimesh.InstanceCount = paths.Count * 2;

		int lineInstanceCount = 0;
		int pointInstanceCount = 0;

		for (int i = 0; i < paths.Count; i++)
		{
			List<Simplex> path = paths[i];

			DrawSimplex(path[0], ref pointInstanceCount);
			DrawSimplex(path[^1], ref pointInstanceCount);

			Color peristenceColor = FloatToColor(Mathf.Abs(path[0].GetAverageValue() - path[^1].GetAverageValue()) * path[0].GetCentroid().DistanceTo(path[^1].GetCentroid()));

			if (!FullPath)
			{
				Vector3 parent = TransformCentroid(path[0].GetCentroid(), SimplexOffset);
				Vector3 child = TransformCentroid(path[^1].GetCentroid(), SimplexOffset);
				parent -= child;

				Lines.Multimesh.SetInstanceTransform(lineInstanceCount, new Transform3D(Basis.Identity, child));
				Lines.Multimesh.SetInstanceCustomData(lineInstanceCount, new Color(parent.X, parent.Y, parent.Z));
				Lines.Multimesh.SetInstanceColor(lineInstanceCount, Colors.White);
				lineInstanceCount++;
			}
			else
			{
				for (int j = 0; j < path.Count - 1; j++)
				{
					Vector3 parent = TransformCentroid(path[j].GetCentroid(), SimplexOffset);
					Vector3 child = TransformCentroid(path[j + 1].GetCentroid(), SimplexOffset);

					parent -= child;
					Lines.Multimesh.SetInstanceTransform(lineInstanceCount, new Transform3D(Basis.Identity, child));
					Lines.Multimesh.SetInstanceCustomData(lineInstanceCount, new Color(parent.X, parent.Y, parent.Z));
					Lines.Multimesh.SetInstanceColor(lineInstanceCount, Colors.White);
					lineInstanceCount++;
				}
			}

		}
	}

	private void DrawSimplexes(HeightmapAnalyzer heightmapAnalyzer)
	{
		List<(Simplex, bool)> simplexes = [.. heightmapAnalyzer.Gradients.Where( x => 
			!x.Value.IsUnassigned() && 
			!x.Value.IsNonCritical() && 
			!x.Value.IsDirectional() &&
			!x.Value.IsIgnored()
		).Select(x => (x.Key, x.Value.IsCritical()))];
		Simplexes.Multimesh.InstanceCount = simplexes.Count;
		int index = 0;
		simplexes.ForEach(x => DrawSimplex(x.Item1, ref index, x.Item2 ? null : Colors.DimGray));

	}

	private void DrawSimplex(Simplex simplex, ref int instanceCount, Color? overrideColor = null)
	{
		Vector3 criticalPoint = TransformCentroid(simplex.GetCentroid(), SimplexOffset);

		Simplexes.Multimesh.SetInstanceTransform(instanceCount, new Transform3D(Basis.Identity.Scaled(Vector3.One * SimplexScale), criticalPoint));

		if (overrideColor == null)
			Simplexes.Multimesh.SetInstanceColor(instanceCount, simplex switch
			{
				Point => Colors.Red,
				Edge => Colors.Green,
				Triangle => Colors.Blue,
				Square => Colors.Blue,
				_ => Colors.Gray
			});
		else 
			Simplexes.Multimesh.SetInstanceColor(instanceCount, (Color)overrideColor);
		instanceCount++;
	}

	public void DrawMorseSmaleComplex(HeightmapAnalyzer heightmapAnalyzer)
	{
		DrawGradientArrows(heightmapAnalyzer);
		// DrawSimplexes(heightmapAnalyzer);
		DrawManifold(heightmapAnalyzer);
	}

	private Vector3 TransformCentroid(Vector3 position, float offset)
	{
		position.X *= 2;
		position.Y *= HeightMapScale + offset;
		position.Z *= 2;
		return position - new Vector3(_size.X - 1, 0, _size.Y - 1);
	}

	public static Color ToColor(Direction direction)
	{
		return direction switch
		{
			Direction.UP => Colors.Blue,
			Direction.DOWN => Colors.Yellow,
			Direction.LEFT => Colors.Green,
			Direction.RIGHT => Colors.Red,
			Direction.TOP_LEFT => Colors.Cyan,
			Direction.TOP_RIGHT => Colors.Purple,
			Direction.BOTTOM_LEFT => Colors.Brown,
			Direction.BOTTOM_RIGHT => Colors.Orange,
			_ => Colors.White
		};
	}

	public static Color FloatToColor(float value)
	{
		return new Color(value, 0, 0, 1f);
	}

}
