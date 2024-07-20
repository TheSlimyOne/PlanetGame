using Godot;
using Godot.Collections;
using System;
using System.Linq;
using ComputeShaderClasses;

public partial class Surface : MultiMeshInstance3D
{
	[ExportGroup("Required")]
	[Export] SurfaceController SurfaceController;
	[Export] CameraController Camera;
	[Export] ShaderMaterial _material;

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

		_material.SetShaderParameter("key_image", _displayKeyData);
		_material.SetShaderParameter("global_key_data", _globalKeyData);
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
			_material.SetShaderParameter("is_debug", SurfaceController.PlanetData.DebugMode);
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
		mesh.SurfaceSetMaterial(0, _material);


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
		if (_material == null || planetData == null) return;

		_material.SetShaderParameter("position_list", _trianglePoints);
		_material.SetShaderParameter("height_gradient", planetData.HeightGradient);
		_material.SetShaderParameter("radius", planetData.Radius);
		_material.SetShaderParameter("albedo_map", planetData.AlbedoMap);
		_material.SetShaderParameter("is_texture_1D", planetData.AlbedoMap is GradientTexture1D);
		_material.SetShaderParameter("height_map", planetData.HeightMap);
		_material.SetShaderParameter("height_scale", planetData.HeightScale);
		_material.SetShaderParameter("is_debug", planetData.DebugMode);
		_material.SetShaderParameter("is_cube", planetData.CubeMode);
		_material.SetShaderParameter("resolution", planetData.Resolution);
		_material.SetShaderParameter("normal_strength", planetData.NormalStrength);
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

			[BufferNames.EXTERNAL_DATA] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.EXTERNAL_DATA],
				GetExternalData()
			),

			[BufferNames.DEBUG_DATA] = new StorageBufferUniform(_rd, _uniformNameToBindings[BufferNames.DEBUG_DATA],
				new Func<byte[]>(() =>
				{
					return Utilities.ToBytes<bool>(new bool[] { Engine.IsEditorHint() }).ToArray();
				}).Invoke()
			),

			[BufferNames.HEIGHT_MAP] = new TextureUniform(_rd, _uniformNameToBindings[BufferNames.HEIGHT_MAP],
				SurfaceController.PlanetData.HeightMap),

			[BufferNames.HEIGHT_GRADIENT] = new TextureUniform(_rd, _uniformNameToBindings[BufferNames.HEIGHT_GRADIENT],
				SurfaceController.PlanetData.HeightGradient),

			[BufferNames.KEYS] = new TextureUniform(_rd, _uniformNameToBindings[BufferNames.KEYS], ref _displayKeyData,
				new RDTextureFormat()
				{
					Width = (uint)(Mathf.Sqrt(MaximumNodes) * 1f/2f),
					Height = (uint)(Mathf.Sqrt(MaximumNodes) * 1f/2f),
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
			[BufferNames.GLOBALKEYSDATA] = new TextureUniform(_rd, _uniformNameToBindings[BufferNames.GLOBALKEYSDATA], ref _globalKeyData,
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
			)
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
		data.AddRange(Utilities.ToBytesSingle(
			new Projection(Camera.GlobalTransform.AffineInverse()) * Camera.GetCameraProjection()).ToArray());

		data.AddRange(Utilities.ToBytes<float>(new float[]
		{
			GlobalTransform[0].X, GlobalTransform[1].X, GlobalTransform[2].X, GlobalTransform[3].X,
			GlobalTransform[0].Y, GlobalTransform[1].Y, GlobalTransform[2].Y, GlobalTransform[3].Y,
			GlobalTransform[0].Z, GlobalTransform[1].Z, GlobalTransform[2].Z, GlobalTransform[3].Z,
			0, 0, 0, 1,

			Mathf.DegToRad(Camera.Fov),
			Camera.Far,
			Camera.Near,
			SurfaceController.PlanetData.Radius,
			SurfaceController.PlanetData.SubFactor * SurfaceController.PlanetData.Radius * Scale.X,
			SurfaceController.PlanetData.Resolution,
			0,0,
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

		if (_processing)
		{
			_rd.TextureClear(_computeShaderUniforms[BufferNames.KEYS].Rid, new Color(0, 0, 0, 1), 0, 1, 0, 1);
			UpdateCopy();
			UpdateComputeCull();
			Render();
			UpdateUniforms();
		}
	}

	private void Render()
	{
		if (_rd == null) return;
		_rd.Sync();

		uint[] indices = Utilities.FromBytes<uint>(_rd.BufferGetData(_computeShaderUniforms[BufferNames.INDICES].Rid)).ToArray();
		uint[] primCounts = Utilities.FromBytes<uint>(_rd.BufferGetData(_computeShaderUniforms[BufferNames.ATOMIC_COUNTER].Rid)).ToArray();
		// Key[] data = Utilities.FromBytes<Key>(_rd.BufferGetData(_computeShaderUniforms[BufferNames.WRITE_CULLED_LIST].Rid)).ToArray();
		
		// byte[] data = _rd.TextureGetData(_computeShaderUniforms[BufferNames.GLOBALKEYSDATA].Rid, 0);
		// Image image = Image.CreateFromData(10, 10, false, Image.Format.Rf, data);
		// GD.Print(image.GetPixel(0,0));
		// image.SetPixel(0,0, new Color(1, 0, 1));
		// image.SetPixel(1,1, new Color(1, 0, 1));
		// image.SetPixel(2,2, new Color(1, 0, 1));
		// image.SetPixel(3,3, new Color(1, 0, 1));
		// image.SetPixel(4,4, new Color(1, 0, 1));

		int all = (int)primCounts[indices[1]];
		int loaded = (int)primCounts[indices[1] + 16];

		Camera.UIElements.SetLabelTriangleCount(loaded, all);

		InstanceAllTriangles(loaded);
		// InstanceAllTriangles(unloaded);
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
