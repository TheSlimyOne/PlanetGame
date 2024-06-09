using Godot;
using Godot.Collections;
using System.Threading.Tasks;

[Tool]
public partial class Planet : Node3D
{

	[Export]
	PlanetData PlanetData
	{
		get => _planetData;
		set
		{
			if (_planetData != null && _planetData.IsConnected("changed", Callable.From(UpdateMulitMesh)) && IsNodeReady())
			{
				GD.Print("Disconnecting");
				_planetData.Changed -= UpdateMulitMesh;
			}
			_planetData = value;
			if (_planetData != null && !_planetData.IsConnected("changed", Callable.From(UpdateMulitMesh)) && IsNodeReady())
			{
				GD.Print("Connecting");
				_planetData.Changed += UpdateMulitMesh;
			}
		}
	}
	private PlanetData _planetData;

	[ExportGroup("Player")]
	[Export] public PlayerController PlayerController { get; set; }

	[ExportGroup("Required")]
	[Export] private MultiMeshInstance3D _surface;
	[Export] private CollisionShape3D _heightMapCollider;
	[Export] private Area3D _mouseDetectionArea;
	[Export] private CollisionShape3D _mouseDetectionCollider;
	[Export] private ShaderMaterial _material;

	[ExportGroup("Settings")]
	[Export(PropertyHint.Range, "1, 1000")] private int _updateFrequency = 60;
	[Export(PropertyHint.Range, "24, 65536")] private uint MaximumNodes = 30000;
	[Export(PropertyHint.Range, "24, 65536")] private uint MaximumCollisionNodes = 30000;

	[Export] private Vector2 _xBias = new();
	[Export] private Vector2 _yBias = new();
	[Export] private Vector2 _zBias = new();
	[Export]
	public bool Processing
	{
		get => _processing;
		set
		{
			_processing = value;
			if (IsNodeReady())
			{
				if (_processing)
				{
					
					GD.PrintRich("[color=cyan]Process Loop Started");
					StartProcessLoop();
				}
				else
				{
					GD.PrintRich("[color=cyan]Process Loop Halted");
				}
			}
		}
	}
	private bool _processing;

	[ExportGroup("Shaders")]
	[Export(PropertyHint.File)] private string _computeCullShader;
	[Export(PropertyHint.File)] private string _copyShader;


	[Export] public GravityField GravityField { get; set; }

	private Vector4[] _trianglePoints;

	private RenderingDevice _rd;

	private Rid _uniformSet_CC;
	private Rid _shader_CC;
	private Rid _pipeline_CC;

	private Rid _uniformSet_C;
	private Rid _shader_C;
	private Rid _pipeline_C;

	private Array<RDUniform> _bindings_CC = new();
	private Array<RDUniform> _bindings_C = new();

	private Rid _atomicCounterBuffer;
	private Rid _indicesBlockBuffer;
	private Rid _readList;
	private Rid _writeFullList;
	private Rid _writeCulledList;
	private Rid _writeCollisionList;
	private Rid _writeExternalPositionsList;
	private Rid _positions;
	private Rid _cameraData;
	private Rid _debug;
	private Rid _dispatchIndirectBuffer;
	private Rid _heightMap;
	private Rid _heightGradient;

	public override void _Input(InputEvent @event)
	{
		if (Engine.IsEditorHint()) return;

		if (@event.IsActionPressed("step"))
		{
			Processing = !Processing;
		}
	}

	public override void _Ready()
	{
		CreateTrianglePoints();
		if (PlanetData == null) return;
	
		SetupComputeShader();
		UpdateMulitMesh();
		Processing = true;
	}

	#region SHADER SETUP

	public override void _Notification(int what)
	{

		//  || what == NotificationEditorPreSave
		// if (what == NotificationWMCloseRequest || what == NotificationPredelete)
		// {
			
		// }

	}

	public void CreateTrianglePoints()
	{
		_trianglePoints = new Vector4[30];
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
			Vector3 axisA = new Vector3(normal.Y, normal.Z, normal.X);
			Vector3 axisB = normal.Cross(axisA);

			_trianglePoints[5 * i + 0] = Vector3Utils.toVector4(normal, 1);
			_trianglePoints[5 * i + 1] = Vector3Utils.toVector4(-axisA + axisB + normal, 1);
			_trianglePoints[5 * i + 2] = Vector3Utils.toVector4(-axisA - axisB + normal, 1);
			_trianglePoints[5 * i + 3] = Vector3Utils.toVector4(axisA + axisB + normal, 1);
			_trianglePoints[5 * i + 4] = Vector3Utils.toVector4(axisA - axisB + normal, 1);
		}
	}

	public void SetMaterialParameters()
	{
		if (_material == null || _planetData == null) return;

		_material.SetShaderParameter("position_list", _trianglePoints);
		_material.SetShaderParameter("height_gradient", _planetData.HeightGradient);
		_material.SetShaderParameter("radius", _planetData.Radius);
		_material.SetShaderParameter("albedo_map", _planetData.AlbedoMap);
		_material.SetShaderParameter("is_texture_1D", _planetData.AlbedoMap is GradientTexture1D);
		_material.SetShaderParameter("height_map", _planetData.HeightMap);
		_material.SetShaderParameter("height_scale", _planetData.HeightScale);
		_material.SetShaderParameter("is_debug", _planetData.DebugMode);
		_material.SetShaderParameter("is_cube", _planetData.CubeMode);
		_material.SetShaderParameter("resolution", _planetData.Resolution);
		_material.SetShaderParameter("normal_strength", _planetData.NormalStrength);
	}

	public void UpdateMulitMesh()
	{
		GD.Print("Mesh was updated");
		if (_planetData == null || !IsNodeReady()) return;

		Vector3[] vertices = new Vector3[_planetData.Resolution * (_planetData.Resolution + 1) / 2];
		Vector3[] normals = new Vector3[_planetData.Resolution * (_planetData.Resolution + 1) / 2];
		Vector2[] uvs = new Vector2[_planetData.Resolution * (_planetData.Resolution + 1) / 2];
		int[] triangles = new int[(_planetData.Resolution - 1) * (_planetData.Resolution - 1) * 6 / 2];
		Vector3 normal = Vector3.Back;
		Vector3 axisA = new Vector3(normal.Y, normal.Z, normal.X);
		Vector3 axisB = normal.Cross(axisA).Abs();
		int triIndex = 0;
		int vertexIndex = 0;

		for (int y = 0; y < _planetData.Resolution; y++)
		{
			for (int x = 0; x < _planetData.Resolution - y; x++)
			{
				int currentIndex = vertexIndex++;
				Vector2 percentage = new Vector2(x, y) / (_planetData.Resolution - 1);
				vertices[currentIndex] = normal + (percentage.X * axisA + percentage.Y * axisB);
				uvs[currentIndex] = new Vector2(x, y);
				normals[currentIndex] = normal;

				if (x != _planetData.Resolution - y - 1)
				{
					if (x == _planetData.Resolution - y - 2)
					{
						triangles[triIndex++] = currentIndex;
						triangles[triIndex++] = currentIndex + 1;
						triangles[triIndex++] = currentIndex + _planetData.Resolution - y;
					}
					else
					{
						bool isXEven = x % 2 == 0;
						bool isYEven = y % 2 == 0;

						if ((isXEven && isYEven) || (!isXEven && !isYEven))
						{
							triangles[triIndex++] = currentIndex;
							triangles[triIndex++] = currentIndex + _planetData.Resolution - y + 1;
							triangles[triIndex++] = currentIndex + _planetData.Resolution - y;
							triangles[triIndex++] = currentIndex;
							triangles[triIndex++] = currentIndex + 1;
							triangles[triIndex++] = currentIndex + _planetData.Resolution - y + 1;
						}
						else
						{
							triangles[triIndex++] = currentIndex;
							triangles[triIndex++] = currentIndex + 1;
							triangles[triIndex++] = currentIndex + _planetData.Resolution - y;
							triangles[triIndex++] = currentIndex + 1;
							triangles[triIndex++] = currentIndex + _planetData.Resolution - y + 1;
							triangles[triIndex++] = currentIndex + _planetData.Resolution - y;
						}
					}
				}
			}
		}

		ArrayMesh mesh = new();
		Array arrays = new();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Index] = triangles;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;

		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.SurfaceSetMaterial(0, _material);

		_surface.Multimesh = new MultiMesh();

		_surface.Multimesh.InstanceCount = 0;
		_surface.Multimesh.Mesh = mesh;
		_surface.Multimesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		_surface.Multimesh.UseCustomData = true;
		_surface.Multimesh.UseColors = true;


		_surface.ExtraCullMargin = 2 * _planetData.Radius;

		// _mouseDetectionCollider.Shape = new BoxShape3D() { Size = Vector3.One * 2 * PlanetData.Radius };
		_mouseDetectionCollider.Shape = new SphereShape3D() { Radius = PlanetData.Radius };


		SetMaterialParameters();
	}

	public void SetupComputeShader()
	{
		if (_rd != null) return;
		GD.Print("Creating Rendering device");
		CreateRenderingDevice();

		// Compute & Cull
		_shader_CC = CreateShader(_computeCullShader);
		_pipeline_CC = CreatePipeline(_shader_CC);

		// Copy
		_shader_C = CreateShader(_copyShader);
		_pipeline_C = CreatePipeline(_shader_C);

		CreateUniforms();
	}

	private void CreateRenderingDevice()
	{
		_rd = RenderingServer.CreateLocalRenderingDevice();
	}

	private Rid CreateShader(string path)
	{
		RDShaderFile shaderFile = GD.Load<RDShaderFile>(path);
		RDShaderSpirV spirV = shaderFile.GetSpirV();
		return _rd.ShaderCreateFromSpirV(spirV);
	}

	private Rid CreatePipeline(Rid shader)
	{
		return _rd.ComputePipelineCreate(shader);
	}

	#region CREATE UNIFORMS
	private (RDUniform, Rid) CreateUniformBufferFromData(byte[] data, int binding, int indirect = 0)
	{
		Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data, usage: (RenderingDevice.StorageBufferUsage)indirect);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};

		uniform.AddId(buffer);
		return (uniform, buffer);
	}

	private void CreateUniforms()
	{
		CreateAtomicCounter(0);
		CreateIndicesBlock(1);
		CreateReadList(2);
		CreateWriteFullList(3);
		CreateWriteCulledList(4);
		CreateWriteCollisionList(5);
		CreatePositionList(6);
		CreateCameraData(7);
		CreateDebugList(8);
		CreateHeightMapBuffer(9);
		CreateHeightGradientBuffer(10);
		CreateExternalPositionsList(11);

		CreateDispatchOutBuffer(2);

		_uniformSet_C = _rd.UniformSetCreate(_bindings_C, _shader_C, 0);
		_uniformSet_CC = _rd.UniformSetCreate(_bindings_CC, _shader_CC, 0);
	}

	private void CreateAtomicCounter(int binding)
	{
		// Atomic Counter
		// Full      list  0 - 15
		// Culling   list 16 - 31
		// Collision list 32 - 47
		uint[] primCounts = new uint[3 * 16];
		primCounts[0] = 6 * 4;
		byte[] data = Utilities.ToBytes<uint>(primCounts).ToArray();

		(RDUniform uniform, _atomicCounterBuffer) = CreateUniformBufferFromData(data, binding);
		_bindings_C.Add(uniform);
		_bindings_CC.Add(uniform);
	}

	private void CreateIndicesBlock(int binding)
	{
		uint[] indices = new uint[] { 0, 1, 8, MaximumNodes };
		byte[] data = Utilities.ToBytes<uint>(indices).ToArray();

		(RDUniform uniform, _indicesBlockBuffer) = CreateUniformBufferFromData(data, binding);
		_bindings_C.Add(uniform);
		_bindings_CC.Add(uniform);
	}

	private void CreateReadList(int binding)
	{
		Key[] readList = new Key[MaximumNodes];

		// Generate cube
		// key = uvec4(nodeIDMSB, nodeIDLSB, meshPolygonID, flagsAndRootID)
		for (int i = 0; i < 6; i++)
		{
			readList[4 * i + 0] = new Key(0, 1, i, 0);
			readList[4 * i + 1] = new Key(0, 1, i, 1);
			readList[4 * i + 2] = new Key(0, 1, i, 2);
			readList[4 * i + 3] = new Key(0, 1, i, 3);
		}

		byte[] data = Utilities.ToBytes<Key>(readList).ToArray();

		(RDUniform uniform, _readList) = CreateUniformBufferFromData(data, binding);
		_bindings_CC.Add(uniform);
	}

	private void CreatePositionList(int binding)
	{
		byte[] data = Utilities.ToBytes<Vector4>(_trianglePoints).ToArray();

		(RDUniform uniform, _positions) = CreateUniformBufferFromData(data, binding);
		_bindings_CC.Add(uniform);
	}

	private void CreateWriteFullList(int binding)
	{
		Key[] writeList = new Key[MaximumNodes];

		byte[] data = Utilities.ToBytes<Key>(writeList).ToArray();

		(RDUniform uniform, _writeFullList) = CreateUniformBufferFromData(data, binding);
		_bindings_CC.Add(uniform);
	}

	private void CreateWriteCulledList(int binding)
	{
		Key[] writeList = new Key[MaximumNodes];

		byte[] data = Utilities.ToBytes<Key>(writeList).ToArray();

		(RDUniform uniform, _writeCulledList) = CreateUniformBufferFromData(data, binding);
		_bindings_CC.Add(uniform);
	}

	private void CreateWriteCollisionList(int binding)
	{
		byte[] data = Utilities.ToBytes<Key>(new Key[MaximumCollisionNodes]).ToArray();
		
		(RDUniform uniform, _writeCollisionList) = CreateUniformBufferFromData(data, binding);
		_bindings_CC.Add(uniform);
	}

	private void CreateExternalPositionsList(int binding)
	{
		Vector3 mouseIntersection = Vector3.Inf;
		Vector3 playerPosition = Vector3.Inf;
		if (PlayerController != null)
		{
			mouseIntersection = PlayerController.GetMouseIntersection();
			playerPosition = PlayerController.GetCameraPosition();
		}

		byte[] data = Utilities.ToBytes<Vector4>(new Vector4[]
		{
			Vector3Utils.toVector4(GlobalPosition, 0),
			Vector3Utils.toVector4(playerPosition, 0),
			Vector3Utils.toVector4(mouseIntersection, 0),

		}).ToArray();

		(RDUniform uniform, _writeExternalPositionsList) = CreateUniformBufferFromData(data, binding);
		_bindings_CC.Add(uniform);
	}

	private void CreateDebugList(int binding)
	{
		// byte[] data = Utilities.ToBytes<bool>(new bool[] { Engine.IsEditorHint() }).ToArray();
		byte[] data = Utilities.ToBytes<Vector4>(new Vector4[MaximumNodes]).ToArray();

		(RDUniform uniform, _debug) = CreateUniformBufferFromData(data, binding);
		_bindings_CC.Add(uniform);
	}

	private void CreateCameraData(int binding)
	{
		Camera3D mainCamera = PlayerController?.Camera ?? null;
		Camera3D helperCamera = PlayerController?.HelperCamera ?? null;

		Transform3D viewMatrix = helperCamera?.GlobalTransform.AffineInverse() ?? GlobalTransform.AffineInverse();
		Projection projectionMatrix = helperCamera?.GetCameraProjection() ?? Projection.Identity;

		byte[] data = Utilities.ToBytes<float>(new float[]
		{
			viewMatrix[0].X, viewMatrix[0].Y, viewMatrix[0].Z, 0,
			viewMatrix[1].X, viewMatrix[1].Y, viewMatrix[1].Z, 0,
			viewMatrix[2].X, viewMatrix[2].Y, viewMatrix[2].Z, 0,
			viewMatrix[3].X, viewMatrix[3].Y, viewMatrix[3].Z, 1,

			projectionMatrix[0].X, projectionMatrix[0].Y, projectionMatrix[0].Z, projectionMatrix[0].W,
			projectionMatrix[1].X, projectionMatrix[1].Y, projectionMatrix[1].Z, projectionMatrix[1].W,
			projectionMatrix[2].X, projectionMatrix[2].Y, projectionMatrix[2].Z, projectionMatrix[2].W,
			projectionMatrix[3].X, projectionMatrix[3].Y, projectionMatrix[3].Z, projectionMatrix[3].W,

			Mathf.DegToRad(mainCamera?.Fov ?? 75), 
			mainCamera?.Far ?? 4000,
			mainCamera?.Near ?? 0.05f,
			PlanetData.Radius,
			PlanetData.SubFactor * PlanetData.Radius, 
			PlanetData.Resolution,
			0,0,
			_xBias.X, _xBias.Y,
			_yBias.X, _yBias.Y,
			_zBias.X, _zBias.Y,
			0,0
		}).ToArray();

		(RDUniform uniform, _cameraData) = CreateUniformBufferFromData(data, binding);
		_bindings_CC.Add(uniform);
	}

	private void CreateDispatchOutBuffer(int binding)
	{
		uint[] workgroups = new uint[] { 1, 1, 1 };

		(RDUniform uniform, _dispatchIndirectBuffer) = CreateUniformBufferFromData(Utilities.ToBytes<uint>(workgroups).ToArray(), binding, 1);
		_bindings_C.Add(uniform);
	}

	private void CreateHeightMapBuffer(int binding)
	{
		RenderingDevice.TextureUsageBits textureUsage = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit;
		RDTextureView view = new();
		Image image = RenderingServer.Texture2DGet(PlanetData.HeightMap.GetRid());

		image.ClearMipmaps();
		image.Convert(Image.Format.L8);

		Array<byte[]> data = new() { image.GetData() };
		RDTextureFormat format = new()
		{
			Width = (uint)image.GetWidth(),
			Height = (uint)image.GetHeight(),
			Format = RenderingDevice.DataFormat.R8Unorm,
			UsageBits = textureUsage
		};

		_heightMap = _rd.TextureCreate(format, view, data);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = binding
		};

		uniform.AddId(_heightMap);
		_bindings_CC.Add(uniform);
	}

	private void CreateHeightGradientBuffer(int binding)
	{
		RenderingDevice.TextureUsageBits textureUsage = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit;
		RDTextureView view = new();
		Image image = RenderingServer.Texture2DGet(PlanetData.HeightGradient.GetRid());
		image.ClearMipmaps();
		image.Convert(Image.Format.L8);

		Array<byte[]> data = new() { image.GetData() };
		RDTextureFormat format = new()
		{
			Width = (uint)image.GetWidth(),
			Height = (uint)image.GetHeight(),
			Format = RenderingDevice.DataFormat.R8Unorm,
			UsageBits = textureUsage
		};

		_heightGradient = _rd.TextureCreate(format, view, data);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = binding
		};

		uniform.AddId(_heightGradient);
		_bindings_CC.Add(uniform);
	}

	#endregion

	#region UPDATE UNIFORMS
	private void UpdateCameraData()
	{
		Camera3D mainCamera = PlayerController?.Camera ?? null;
		Camera3D helperCamera = PlayerController?.HelperCamera ?? null;

		Transform3D viewMatrix = helperCamera?.GlobalTransform.AffineInverse() ?? GlobalTransform.AffineInverse();
		Projection projectionMatrix = helperCamera?.GetCameraProjection() ?? Projection.Identity;

		byte[] data = Utilities.ToBytes<float>(new float[]
		{
			viewMatrix[0].X, viewMatrix[0].Y, viewMatrix[0].Z, 0,
			viewMatrix[1].X, viewMatrix[1].Y, viewMatrix[1].Z, 0,
			viewMatrix[2].X, viewMatrix[2].Y, viewMatrix[2].Z, 0,
			viewMatrix[3].X, viewMatrix[3].Y, viewMatrix[3].Z, 1,

			projectionMatrix[0].X, projectionMatrix[0].Y, projectionMatrix[0].Z, projectionMatrix[0].W,
			projectionMatrix[1].X, projectionMatrix[1].Y, projectionMatrix[1].Z, projectionMatrix[1].W,
			projectionMatrix[2].X, projectionMatrix[2].Y, projectionMatrix[2].Z, projectionMatrix[2].W,
			projectionMatrix[3].X, projectionMatrix[3].Y, projectionMatrix[3].Z, projectionMatrix[3].W,

			Mathf.DegToRad(mainCamera?.Fov ?? 75), 
			mainCamera?.Far ?? 4000,
			mainCamera?.Near ?? 0.05f,
			PlanetData.Radius,
			PlanetData.SubFactor * PlanetData.Radius, 
			PlanetData.Resolution,
			0,0,
			_xBias.X, _xBias.Y,
			_yBias.X, _yBias.Y,
			_zBias.X, _zBias.Y,
			0,0
		}).ToArray();

		_rd.BufferUpdate(_cameraData, 0, (uint)data.Length, data);
	}

	private void UpdateReadList()
	{
		byte[] data = _rd.BufferGetData(_writeFullList);
		_rd.BufferUpdate(_readList, 0, (uint)data.Length, data);
	}

	private void UpdateWriteFullList()
	{
		byte[] data = Utilities.ToBytes<Key>(new Key[MaximumNodes]).ToArray();
		_rd.BufferUpdate(_writeFullList, 0, (uint)data.Length, data);
	}

	private void UpdateWriteCulledList()
	{
		byte[] data = Utilities.ToBytes<Key>(new Key[MaximumNodes]).ToArray();
		_rd.BufferUpdate(_writeFullList, 0, (uint)data.Length, data);
	}

	private void UpdateWriteCollisionList()
	{
		byte[] data = Utilities.ToBytes<Key>(new Key[MaximumCollisionNodes]).ToArray();
		_rd.BufferUpdate(_writeCollisionList, 0, (uint)data.Length, data);
	}

	private void UpdateExternalPositionsList()
	{
		Vector3 mouseIntersection = Vector3.Inf;
		Vector3 playerPosition = Vector3.Inf;
		if (PlayerController != null)
		{
			mouseIntersection = PlayerController.GetMouseIntersection();
			playerPosition = PlayerController.GetCameraPosition();
		}

		byte[] data = Utilities.ToBytes<Vector4>(new Vector4[]
		{
			Vector3Utils.toVector4(GlobalPosition, 0),
			Vector3Utils.toVector4(playerPosition, 0),
			Vector3Utils.toVector4(mouseIntersection, 0),

		}).ToArray();

		_rd.BufferUpdate(_writeExternalPositionsList, 0, (uint)data.Length, data);
	}

	private void UpdateIndicesBlock()
	{
		uint[] indices = Utilities.FromBytes<uint>(_rd.BufferGetData(_indicesBlockBuffer)).ToArray();
		indices[0] = (indices[0] + 1) % 16; // Read Index
		indices[1] = (indices[1] + 1) % 16; // Write Index
		indices[2] = (indices[2] + 1) % 16; // Delete Index
		indices[3] = MaximumNodes;
		byte[] data = Utilities.ToBytes<uint>(indices).ToArray();

		_rd.BufferUpdate(_indicesBlockBuffer, 0, (uint)data.Length, data);
	}

	private void UpdateUniforms()
	{
		if (_rd == null) return;
		UpdateIndicesBlock();
		UpdateReadList();
		UpdateWriteFullList();
		UpdateCameraData();
		UpdateWriteCulledList();
		UpdateWriteCollisionList();
		UpdateExternalPositionsList();
	}

	#endregion

	#endregion

	#region PROCESSING

	async public void StartProcessLoop()
	{
		if (PlanetData == null) return;
		if (_rd == null) SetupComputeShader();

		while (Processing)
		{
			UpdateCopy();
			UpdateComputeCull();
			await Task.Delay(_updateFrequency);
			Render();
			UpdateUniforms();

			if (Engine.IsEditorHint())
			{
				GD.Print("Ran in editor");
				Processing = false;
			}
		}
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
		_rd.ComputeListDispatchIndirect(computeList, _dispatchIndirectBuffer, 0);
		_rd.ComputeListEnd();
		_rd.Submit();
	}

	private void Render()
	{
		if (_rd == null) return;
		_rd.Sync();

		Key[] keys = Utilities.FromBytes<Key>(_rd.BufferGetData(_writeCulledList)).ToArray();
		// Key[] keys = Utilities.FromBytes<Key>(_rd.BufferGetData(_writeCollisionList)).ToArray();
		uint[] indices = Utilities.FromBytes<uint>(_rd.BufferGetData(_indicesBlockBuffer)).ToArray();
		uint[] primCounts = Utilities.FromBytes<uint>(_rd.BufferGetData(_atomicCounterBuffer)).ToArray();
		Vector4[] debug = Utilities.FromBytes<Vector4>(_rd.BufferGetData(_debug)).ToArray();

		int loaded = (int)primCounts[indices[1] + 16];
		int unloaded = (int)primCounts[indices[1]];

		PlayerController?.SetLabelTriangleCount(loaded, unloaded);
		InstanceAllTriangles(keys, loaded, debug);
	}

	public void InstanceAllTriangles(Key[] keys, int amount, Vector4[] debug)
	{
		// TODO make this better?
		_surface.Multimesh.InstanceCount = 0;
		_surface.Multimesh.InstanceCount = amount;
		
		if (amount > keys.Length)
		{
			GD.PrintErr($"Array is not large enough to hold: {amount}, currently: {MaximumNodes}");
			Processing = false;
			return;
		}

		

		for (int i = 0; i < amount; i++)
		{
			
			// GD.Print(keys[i]);
			Transform3D transform = new Transform3D(Basis.Identity, Vector3.Zero);
			_surface.Multimesh.SetInstanceTransform(i, transform);
			_surface.Multimesh.SetInstanceCustomData(i, keys[i].ToColor());
			// _surface.Multimesh.SetInstanceColor(i, new Color(debug[i].X,debug[i].Y,debug[i].Z,debug[i].W));
		}
		
	}

	private void CleanupGPU()
	{
		if (_rd == null) return;

		GD.PrintRich("[color=red]Cleaning up GPU");
		_rd.FreeRid(_uniformSet_C);
		_rd.FreeRid(_pipeline_C);
		_rd.FreeRid(_shader_C);
		_rd.FreeRid(_uniformSet_CC);
		_rd.FreeRid(_pipeline_CC);
		_rd.FreeRid(_shader_CC);

		

		if (_atomicCounterBuffer.Id != 0)
			_rd.FreeRid(_atomicCounterBuffer);
		if (_indicesBlockBuffer.Id != 0)
			_rd.FreeRid(_indicesBlockBuffer);
		if (_readList.Id != 0)
			_rd.FreeRid(_readList);
		if (_writeFullList.Id != 0)
			_rd.FreeRid(_writeFullList);
		if (_writeCulledList.Id != 0)
			_rd.FreeRid(_writeCulledList);
		if (_writeCollisionList.Id != 0)
			_rd.FreeRid(_writeCollisionList);
		if (_writeExternalPositionsList.Id != 0)
			_rd.FreeRid(_writeExternalPositionsList);
		if (_positions.Id != 0)
			_rd.FreeRid(_positions);
		if (_cameraData.Id != 0)
			_rd.FreeRid(_cameraData);
		if (_debug.Id != 0)
			_rd.FreeRid(_debug);
		if (_dispatchIndirectBuffer.Id != 0)
			_rd.FreeRid(_dispatchIndirectBuffer);
		if (_heightMap.Id != 0)
			_rd.FreeRid(_heightMap);
		if (_heightGradient.Id != 0)
			_rd.FreeRid(_heightGradient);

		_rd.Free();
		_rd = null;
	}

	#endregion

	public override void _EnterTree()
	{
		if (_planetData != null && !_planetData.IsConnected("changed", Callable.From(UpdateMulitMesh)))
		{
			GD.Print("Connecting");
			_planetData.Changed += UpdateMulitMesh;
		}
	}

	public override void _ExitTree()
	{
		if (_planetData != null && _planetData.IsConnected("changed", Callable.From(UpdateMulitMesh)))
		{
			GD.Print("Disconnecting");
			_planetData.Changed -= UpdateMulitMesh;
		}

		CleanupGPU();
	}
}