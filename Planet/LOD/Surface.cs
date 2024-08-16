using Godot;
using Godot.Collections;
using System;
using System.Linq;
using ComputeShaderClasses;
using System.Xml.Serialization;

public partial class Surface : MultiMeshInstance3D
{
	[ExportGroup("Required")]
	[Export] SurfaceController SurfaceController;
	[Export] CameraController Camera;
	[Export] public ShaderMaterial Material;

	[ExportGroup("Compute Shader Settings")]
	[Export] uint MaximumNodes;
	[Export(PropertyHint.Range, "1, 1000")] private int _updateFrequency = 60;

	[ExportGroup("Shaders")]
	[Export(PropertyHint.File)] private string _computeCullShader;
	[Export(PropertyHint.File)] private string _copyShader;


	private RenderingDevice _rd;
	private Rid _uniformSet_CC;
	private Rid _shader_CC;
	private Rid _pipeline_CC;

	private Texture2Drd _displayKeyData;
	private Texture2Drd _globalKeyData;

	private Array<RDUniform> _bindings_CC = new();
	private Array<RDUniform> _bindings_C = new();

	private Rid _uniformSet_C;
	private Rid _shader_C;
	private Rid _pipeline_C;

	private bool _processing;

	public enum BufferNames
	{
		ATOMIC_COUNTER,
		INDICES,
		READ_LIST,
		DISPATCH_BUFFER,
		WRITE_FULL_LIST,
		WRITE_CULLED_LIST,
		TRIANGLE_COORDINATES,
		EXTERNAL_DATA,
		DEBUG_DATA,
		HEIGHT_MAP,
		HEIGHT_GRADIENT,
		KEYS,
		GLOBALKEYSDATA
	}

	private readonly Dictionary<BufferNames, int> _uniformNameToBindings = new()
	{
		[BufferNames.ATOMIC_COUNTER] = 0,
		[BufferNames.INDICES] = 1,
		[BufferNames.READ_LIST] = 2,
		[BufferNames.DISPATCH_BUFFER] = 2,
		[BufferNames.GLOBALKEYSDATA] = 3,
		[BufferNames.WRITE_FULL_LIST] = 4,
		[BufferNames.WRITE_CULLED_LIST] = 5,
		[BufferNames.TRIANGLE_COORDINATES] = 6,
		[BufferNames.EXTERNAL_DATA] = 7,
		[BufferNames.DEBUG_DATA] = 8,
		[BufferNames.HEIGHT_MAP] = 9,
		[BufferNames.HEIGHT_GRADIENT] = 10,
		[BufferNames.KEYS] = 11,
	};

	private Vector4[] _trianglePoints;
	private Dictionary<BufferNames, ComputeShaderUniform> _computeShaderUniforms;

	public override void _Ready()
	{
		CreateTrianglePoints();


		if (SurfaceController.PlanetData == null) return;
		SetupComputeShader();
		UpdateMulitMesh();

		Material.SetShaderParameter("key_image", _displayKeyData);
		Material.SetShaderParameter("global_key_data", _globalKeyData);
		Camera.GetChild(0).GetChild<TextureRect>(1).Texture = _displayKeyData;
		_processing = true;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest || what == NotificationPredelete)
		{
			_processing = false;
			CleanupGPU();
		}

	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("step"))
		{
			_processing = !_processing;
		}
		if (@event.IsActionPressed("debug_mode"))
		{
			SurfaceController.PlanetData.DebugMode = !SurfaceController.PlanetData.DebugMode;
			Material.SetShaderParameter("is_debug", SurfaceController.PlanetData.DebugMode);
		}
		if (@event.IsActionPressed("cube_mode"))
		{
			SurfaceController.PlanetData.CubeMode = !SurfaceController.PlanetData.CubeMode;
			Material.SetShaderParameter("is_cube", SurfaceController.PlanetData.CubeMode);
		}
	}

	public void CreateTrianglePoints()
	{
		_trianglePoints = new Vector4[6 * 5];
		Vector3[] normals = new Vector3[]
		{
			Vector3.Up,
			Vector3.Down,
			Vector3.Right,
			Vector3.Left,
			Vector3.Forward,
			Vector3.Back,
		};

		for (int i = 0; i < 6; i++)
		{
			Vector3 normal = normals[i];
			Vector3 axisA = new(normal.Y, normal.Z, normal.X);
			Vector3 axisB = normal.Cross(axisA);

			_trianglePoints[5 * i + 0] = VectorUtils.toVector4(normal, 1);
			_trianglePoints[5 * i + 1] = VectorUtils.toVector4(-axisA + axisB + normal, 1);
			_trianglePoints[5 * i + 2] = VectorUtils.toVector4(-axisA - axisB + normal, 1);
			_trianglePoints[5 * i + 3] = VectorUtils.toVector4(axisA + axisB + normal, 1);
			_trianglePoints[5 * i + 4] = VectorUtils.toVector4(axisA - axisB + normal, 1);
		}
	}

	public void UpdateMulitMesh()
	{
		PlanetData planetData = SurfaceController.PlanetData;
		if (planetData == null) return;

		Vector3[] vertices = new Vector3[planetData.Resolution * (planetData.Resolution + 1) / 2];
		Vector3[] normals = new Vector3[planetData.Resolution * (planetData.Resolution + 1) / 2];
		Vector2[] uvs = new Vector2[planetData.Resolution * (planetData.Resolution + 1) / 2];
		int[] triangles = new int[(planetData.Resolution - 1) * (planetData.Resolution - 1) * 6 / 2];
		Vector3 normal = Vector3.Back;
		Vector3 axisA = new(normal.Y, normal.Z, normal.X);
		Vector3 axisB = normal.Cross(axisA).Abs();
		int triIndex = 0;
		int vertexIndex = 0;

		for (int y = 0; y < planetData.Resolution; y++)
		{
			for (int x = 0; x < planetData.Resolution - y; x++)
			{
				int currentIndex = vertexIndex++;
				Vector2 percentage = new Vector2(x, y) / (planetData.Resolution - 1);
				vertices[currentIndex] = normal + (percentage.X * axisA + percentage.Y * axisB);
				uvs[currentIndex] = new Vector2(x, y);
				normals[currentIndex] = normal;

				if (x != planetData.Resolution - y - 1)
				{
					if (x == planetData.Resolution - y - 2)
					{
						triangles[triIndex++] = currentIndex;
						triangles[triIndex++] = currentIndex + 1;
						triangles[triIndex++] = currentIndex + planetData.Resolution - y;
					}
					else
					{
						bool isXEven = x % 2 == 0;
						bool isYEven = y % 2 == 0;

						if ((isXEven && isYEven) || (!isXEven && !isYEven))
						{
							triangles[triIndex++] = currentIndex;
							triangles[triIndex++] = currentIndex + planetData.Resolution - y + 1;
							triangles[triIndex++] = currentIndex + planetData.Resolution - y;
							triangles[triIndex++] = currentIndex;
							triangles[triIndex++] = currentIndex + 1;
							triangles[triIndex++] = currentIndex + planetData.Resolution - y + 1;
						}
						else
						{
							triangles[triIndex++] = currentIndex;
							triangles[triIndex++] = currentIndex + 1;
							triangles[triIndex++] = currentIndex + planetData.Resolution - y;
							triangles[triIndex++] = currentIndex + 1;
							triangles[triIndex++] = currentIndex + planetData.Resolution - y + 1;
							triangles[triIndex++] = currentIndex + planetData.Resolution - y;
						}
					}
				}
			}
		}

		ArrayMesh mesh = new();
		Godot.Collections.Array arrays = new();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Index] = triangles;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.SurfaceSetMaterial(0, Material);


		Multimesh = new MultiMesh
		{
			InstanceCount = 0,
			Mesh = mesh,
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseCustomData = true,
			UseColors = true
		};

		ExtraCullMargin = 2 * planetData.Radius;
		SetMaterialParameters();
	}

	public void SetMaterialParameters()
	{
		PlanetData planetData = SurfaceController.PlanetData;
		if (Material == null || planetData == null) return;

		Material.SetShaderParameter("position_list", _trianglePoints);
		Material.SetShaderParameter("height_gradient", planetData.HeightGradient);
		Material.SetShaderParameter("radius", planetData.Radius);
		Material.SetShaderParameter("albedo_map", planetData.AlbedoMap);
		Material.SetShaderParameter("is_texture_1D", planetData.AlbedoMap is GradientTexture1D);
		Material.SetShaderParameter("height_map", planetData.HeightMap);
		Material.SetShaderParameter("height_scale", planetData.HeightScale);
		Material.SetShaderParameter("is_debug", planetData.DebugMode);
		Material.SetShaderParameter("is_cube", planetData.CubeMode);
		Material.SetShaderParameter("resolution", planetData.Resolution);
		Material.SetShaderParameter("normal_strength", planetData.NormalStrength);
	}

	private void SetupComputeShader()
	{
		if (_rd != null) return;
		_rd = RenderingServer.GetRenderingDevice();

		// Compute & Cull
		_shader_CC = CreateShader(_computeCullShader);
		_pipeline_CC = CreatePipeline(_shader_CC);

		// Copy
		_shader_C = CreateShader(_copyShader);
		_pipeline_C = CreatePipeline(_shader_C);

		CreateUniforms();
	}

	private Rid CreatePipeline(Rid shader)
	{
		return _rd.ComputePipelineCreate(shader);
	}

	private Rid CreateShader(string path)
	{
		RDShaderFile shaderFile = GD.Load<RDShaderFile>(path);
		RDShaderSpirV spirV = shaderFile.GetSpirV();
		return _rd.ShaderCreateFromSpirV(spirV);
	}

	private void CreateUniforms()
	{
		_computeShaderUniforms = new Dictionary<BufferNames, ComputeShaderUniform>()
		{
			// Full      list  0  - 15
			// Culling   list  16 - 31
			[BufferNames.ATOMIC_COUNTER] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.ATOMIC_COUNTER],
				new Func<byte[]>(() =>
				{
					uint[] primCounts = new uint[2 * 16];
					primCounts[0] = 6 * 4;
					return Utilities.ToBytes<uint>(primCounts).ToArray();
				}).Invoke()
			),

			// 0 Read Index
			// 1 Write Index
			// 2 Delete Index
			// 3 Max nodes
			[BufferNames.INDICES] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.INDICES],
				Utilities.ToBytes<uint>(new uint[] { 0, 1, 8, MaximumNodes }).ToArray()
			),

			// key = uvec4(nodeIDMSB, nodeIDLSB, meshPolygonID, flagsAndRootID)
			[BufferNames.READ_LIST] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.READ_LIST],
				new Func<byte[]>(() =>
				{
					Key[] readList = new Key[MaximumNodes];

					for (int i = 0; i < 6; i++)
					{
						readList[4 * i + 0] = new Key(0, 1, i, 0);
						readList[4 * i + 1] = new Key(0, 1, i, 1);
						readList[4 * i + 2] = new Key(0, 1, i, 2);
						readList[4 * i + 3] = new Key(0, 1, i, 3);
					}
					return Utilities.ToBytes<Key>(readList).ToArray();
				}).Invoke()
			),

			[BufferNames.DISPATCH_BUFFER] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.DISPATCH_BUFFER],
				Utilities.ToBytes<uint>(new uint[] { 1, 1, 1 }).ToArray(), 1
			),

			[BufferNames.WRITE_FULL_LIST] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.WRITE_FULL_LIST],
				Utilities.ToBytes<Key>(new Key[MaximumNodes]).ToArray()
			),

			[BufferNames.WRITE_CULLED_LIST] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.WRITE_CULLED_LIST],
				Utilities.ToBytes<Key>(new Key[MaximumNodes]).ToArray()
			),

			[BufferNames.TRIANGLE_COORDINATES] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.TRIANGLE_COORDINATES],
				Utilities.ToBytes<Vector4>(_trianglePoints).ToArray()
			),

			[BufferNames.DEBUG_DATA] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.DEBUG_DATA],
				new Func<byte[]>(() =>
				{
					return Utilities.ToBytes<bool>(new bool[] { Engine.IsEditorHint() }).ToArray();
				}).Invoke()
			),

			[BufferNames.HEIGHT_MAP] = new Texture2DUniform(_rd, _uniformNameToBindings[BufferNames.HEIGHT_MAP],
				SurfaceController.PlanetData.HeightMap),

			[BufferNames.HEIGHT_GRADIENT] = new Texture2DUniform(_rd, _uniformNameToBindings[BufferNames.HEIGHT_GRADIENT],
				SurfaceController.PlanetData.HeightGradient),

			[BufferNames.KEYS] = new Texture2DUniform(_rd, _uniformNameToBindings[BufferNames.KEYS], ref _displayKeyData,
				new RDTextureFormat()
				{
					Width = (uint)(Mathf.Sqrt(MaximumNodes) * 1f / 2f),
					Height = (uint)(Mathf.Sqrt(MaximumNodes) * 1f / 2f),
					TextureType = RenderingDevice.TextureType.Type2D,
					Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
					UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
								RenderingDevice.TextureUsageBits.StorageBit |
								RenderingDevice.TextureUsageBits.CanUpdateBit |
								RenderingDevice.TextureUsageBits.CanCopyToBit |
								RenderingDevice.TextureUsageBits.CanCopyFromBit |
								RenderingDevice.TextureUsageBits.ColorAttachmentBit

				}
			),

			[BufferNames.GLOBALKEYSDATA] = new Texture2DUniform(_rd, _uniformNameToBindings[BufferNames.GLOBALKEYSDATA], ref _globalKeyData,
				new RDTextureFormat()
				{
					Width = 10u,
					Height = 10u,
					TextureType = RenderingDevice.TextureType.Type2D,
					Format = RenderingDevice.DataFormat.R32Sfloat,
					UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
								RenderingDevice.TextureUsageBits.StorageBit |
								RenderingDevice.TextureUsageBits.CanUpdateBit |
								RenderingDevice.TextureUsageBits.CanCopyToBit |
								RenderingDevice.TextureUsageBits.CanCopyFromBit |
								RenderingDevice.TextureUsageBits.ColorAttachmentBit

				}
			),

			[BufferNames.EXTERNAL_DATA] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.EXTERNAL_DATA],
				GetExternalData()
			),
		};

		_bindings_CC.Add(_computeShaderUniforms[BufferNames.ATOMIC_COUNTER].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.INDICES].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.READ_LIST].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.GLOBALKEYSDATA].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.KEYS].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.WRITE_FULL_LIST].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.WRITE_CULLED_LIST].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.TRIANGLE_COORDINATES].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.EXTERNAL_DATA].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.DEBUG_DATA].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.HEIGHT_MAP].Uniform);
		_bindings_CC.Add(_computeShaderUniforms[BufferNames.HEIGHT_GRADIENT].Uniform);

		_bindings_C.Add(_computeShaderUniforms[BufferNames.ATOMIC_COUNTER].Uniform);
		_bindings_C.Add(_computeShaderUniforms[BufferNames.INDICES].Uniform);
		_bindings_C.Add(_computeShaderUniforms[BufferNames.DISPATCH_BUFFER].Uniform);
		_bindings_C.Add(_computeShaderUniforms[BufferNames.GLOBALKEYSDATA].Uniform);

		_uniformSet_CC = _rd.UniformSetCreate(_bindings_CC, _shader_CC, 0);
		_uniformSet_C = _rd.UniformSetCreate(_bindings_C, _shader_C, 0);
	}

	private void UpdateUniforms()
	{
		
		_computeShaderUniforms[BufferNames.INDICES].UpdateUniform(
			GetIndicesData()
		);
		_computeShaderUniforms[BufferNames.READ_LIST].UpdateUniform(
			_rd.BufferGetData(_computeShaderUniforms[BufferNames.WRITE_FULL_LIST].Rid)
		);

		_computeShaderUniforms[BufferNames.EXTERNAL_DATA].UpdateUniform(
			GetExternalData()
		);
	}

	private byte[] GetIndicesData()
	{
		uint[] indices = Utilities.FromBytes<uint>(_rd.BufferGetData(_computeShaderUniforms[BufferNames.INDICES].Rid)).ToArray();
		indices[0] = (indices[0] + 1) % 16; // Read Index
		indices[1] = (indices[1] + 1) % 16; // Write Index
		indices[2] = (indices[2] + 1) % 16; // Delete Index
		indices[3] = MaximumNodes;
		return Utilities.ToBytes<uint>(indices).ToArray();
	}
	
	private byte[] GetExternalData()
	{
		Array<byte> data = new();
		

		// GD.Print(planetTransform);
		// GD.Print();
		// GD.Print("====================");
		// GD.Print(CreateOffsetMatrix());
		data.AddRange(Utilities.ToBytesSingle(Camera.GetViewProjectionMatrix()).ToArray());
		data.AddRange(Utilities.ToBytesSingle(VectorUtils.toVector4(Camera.GlobalPosition, 0)).ToArray());
		data.AddRange(Utilities.ToBytesSingle(GetPlanetTransformMatrix()).ToArray());
		data.AddRange(Utilities.ToBytes<float>(new float[]
		{
			// planetTransform[0].X, planetTransform[1].X, planetTransform[2].X, planetTransform[3].X,
			// planetTransform[0].Y, planetTransform[1].Y, planetTransform[2].Y, planetTransform[3].Y,
			// planetTransform[0].Z, planetTransform[1].Z, planetTransform[2].Z, planetTransform[3].Z,
			// 0, 0, 0, 1,
			
			Mathf.DegToRad(Camera.Fov),
			SurfaceController.PlanetData.Radius,
			SurfaceController.PlanetData.SubFactor * SurfaceController.PlanetData.Radius,
			SurfaceController.PlanetData.Resolution,
		}).ToArray());
		return data.ToArray();
	}

	private void UpdateCopy()
	{
		if (_rd == null) return;
		long computeList = _rd.ComputeListBegin();
		_rd.ComputeListBindComputePipeline(computeList, _pipeline_C);
		_rd.ComputeListBindUniformSet(computeList, _uniformSet_C, 0);
		_rd.ComputeListDispatch(computeList, 1, 1, 1);
		_rd.ComputeListEnd();
		_rd.Submit();
		_rd.Sync();
	}

	private void UpdateComputeCull()
	{
		if (_rd == null) return;
		long computeList = _rd.ComputeListBegin();
		_rd.ComputeListBindComputePipeline(computeList, _pipeline_CC);
		_rd.ComputeListBindUniformSet(computeList, _uniformSet_CC, 0);
		_rd.ComputeListDispatchIndirect(computeList, _computeShaderUniforms[BufferNames.DISPATCH_BUFFER].Rid, 0);
		_rd.ComputeListEnd();
		_rd.Submit();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (SurfaceController.PlanetData == null || _rd == null) return;
		SetMaterialParameters();
		if (_processing)
		{
			_rd.TextureClear(_computeShaderUniforms[BufferNames.KEYS].Rid, new Color(0, 0, 0, 1), 0, 1, 0, 1);
			UpdateCopy();
			UpdateComputeCull();
			Render();
			UpdateUniforms();
			// _processing = false;
		}
	}

	private void Render()
	{
		if (_rd == null) return;
		_rd.Sync();

		uint[] indices = Utilities.FromBytes<uint>(_rd.BufferGetData(_computeShaderUniforms[BufferNames.INDICES].Rid)).ToArray();
		uint[] primCounts = Utilities.FromBytes<uint>(_rd.BufferGetData(_computeShaderUniforms[BufferNames.ATOMIC_COUNTER].Rid)).ToArray();
		Camera.UIElements.SetCurrentLOD(GetGlobalPixelData(0, 0).R);


		Key[] data = Utilities.FromBytes<Key>(_rd.BufferGetData(_computeShaderUniforms[BufferNames.WRITE_FULL_LIST].Rid)).ToArray();
		// Image image = Image.CreateFromData(10, 10, false, Image.Format.Rf, data);
		// RenderingServer
		// byte[] data = _rd.TextureGetData(_computeShaderUniforms[BufferNames.GLOBALKEYSDATA].Rid, 0);
		// float[] yes = Utilities.FromBytes<float>(data).ToArray();
		// GD.Print(yes[0]);

		int all = (int)primCounts[indices[1]];
		int culled = (int)primCounts[indices[1] + 16];

		Camera.UIElements.SetLabelTriangleCount(culled, all);


		// _processing = false;
		InstanceAllTriangles(culled);
		// InstanceAllTriangles(data, all);
	}

	public void InstanceAllTriangles(Key[] keys, int amount)
	{
		Multimesh.InstanceCount = amount;
		Transform3D transform = new(Basis.Identity, Vector3.Zero);
		for (int i = 0; i < amount; i++)
		{
			Multimesh.SetInstanceTransform(i, transform);
			Multimesh.SetInstanceCustomData(i, keys[i].ToColor());
		}
	}

	public Color GetGlobalPixelData(int x, int y)
	{
		return RenderingServer.Texture2DGet(_globalKeyData.GetRid()).GetPixel(x, y);
	}

	public Projection GetPlanetTransformMatrix()
	{
		float radius = SurfaceController.PlanetData.Radius;
		Vector3 scaleFromPoint = Vector3.Back;

		// Radius isnt applied here future me idk why tho
		return SurfaceController.GetProjection() * new Projection(
			new Vector4(radius, 0, 0, scaleFromPoint.X - radius * scaleFromPoint.X),
			new Vector4(0, radius, 0, scaleFromPoint.Y - radius * scaleFromPoint.Y),
			new Vector4(0, 0, radius, scaleFromPoint.Z - radius * scaleFromPoint.Z - 1),
			new Vector4(0, 0, 0, 1)
		);
	}

	public void InstanceAllTriangles(int amount)
	{
		Multimesh.InstanceCount = amount;
		Transform3D transform = new(Basis.Identity, Vector3.Zero);
		for (int i = 0; i < amount; i++)
		{
			Multimesh.SetInstanceTransform(i, transform);
		}
	}

	public void CleanupGPU()
	{
		if (_rd == null) return;

		foreach (ComputeShaderUniform computeShaderUniform in _computeShaderUniforms.Values)
		{
			computeShaderUniform.FreeRid();
		}

		_rd.FreeRid(_uniformSet_C);
		_rd.FreeRid(_pipeline_C);
		_rd.FreeRid(_shader_C);
		_rd.FreeRid(_uniformSet_CC);
		_rd.FreeRid(_pipeline_CC);
		_rd.FreeRid(_shader_CC);

		_rd.Free();
		_rd = null;
	}
}
