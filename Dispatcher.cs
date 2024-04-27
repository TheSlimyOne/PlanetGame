using Godot;
using Godot.Collections;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public partial class Dispatcher : Node
{
	[ExportGroup("Settings")]
	[Export(PropertyHint.Range, "1, 1000")] private int _updateFrequency = 60;
	[Export] private bool _autoStart;

	[ExportGroup("Requirements")]
	[Export(PropertyHint.File)] private string _computeShader;

	// At the very least must be 24
	public uint MaximumNodes = 30;

	private RenderingDevice _rd;

	private Rid _uniformSet;
	private Rid _shader;
	private Rid _pipeline;

	private RDUniform _inputUniform;
	private RDUniform _outputUniform;
	private Array<RDUniform> _bindings = new();

	private Rid _atomicCounterBuffer;
	private Rid _indicesBlockBuffer;

	private Rid _readList;
	private Rid _writeList;
	private Rid _positions;
	private Rid _data;
	private Rid _cameraData;

	[Export]
	private Camera3D _camera;

	private bool _processing;

	private float radius = 20;

	#region MAIN LOOP
	public override void _Ready()
	{
		SetupComputeShader();
	}
	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("step"))
		{
			if (_processing)
			{
				_processing = false;
				GD.Print("she stop :C");
			}
			else
			{
				StartProcessLoop();
				GD.Print("she go :)");
			}
		}
	}
	public override void _Notification(int what)
	{

	}
	#endregion

	#region SHADER SETUP

	private void CreateRenderingDevice()
	{
		_rd = RenderingServer.CreateLocalRenderingDevice();
	}

	private void CreateShader()
	{
		RDShaderFile shaderFile = GD.Load<RDShaderFile>(_computeShader);
		RDShaderSpirV spirV = shaderFile.GetSpirV();
		_shader = _rd.ShaderCreateFromSpirV(spirV);
	}

	private void CreatePipeline()
	{
		_pipeline = _rd.ComputePipelineCreate(_shader);
	}

	private Rid CreateAtomicCounter(int binding)
	{
		uint[] primCountFullAndCull = new uint[2 * 16];
		primCountFullAndCull[0] = 30;
		byte[] data = ToBytes<uint>(primCountFullAndCull).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};

		uniform.AddId(buffer);
		_bindings.Add(uniform);

		return buffer;
	}

	private Rid CreateIndicesBlock(int binding)
	{

		uint[] indices = new uint[2];
		byte[] data = ToBytes<uint>(indices).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};

		uniform.AddId(buffer);
		_bindings.Add(uniform);

		return buffer;
	}

	private Rid CreateReadList(int binding)
	{
		Vector4I[] readList = new Vector4I[MaximumNodes];

		// Generate cube

		// key = uvec4(nodeID_MSB, nodeID_LSB, meshPolygonID, rootID)
		for (int i = 0; i < 6; i++)
		{
			readList[4 * i + 0] = new Vector4I(0, 1, i, 0);
			readList[4 * i + 1] = new Vector4I(0, 1, i, 1);
			readList[4 * i + 2] = new Vector4I(0, 1, i, 2);
			readList[4 * i + 3] = new Vector4I(0, 1, i, 3);
		}

		byte[] data = ToBytes<Vector4I>(readList).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data);

		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};

		uniform.AddId(buffer);
		_bindings.Add(uniform);

		return buffer;
	}

	private Rid CreatePositionList(int binding)
	{
		Vector4[] positions = new Vector4[30];
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

			positions[5 * i + 0] = Vector3Utils.toVector4(normal, 1);
			positions[5 * i + 1] = Vector3Utils.toVector4(-axisA - axisB + normal, 1);
			positions[5 * i + 2] = Vector3Utils.toVector4(-axisA + axisB + normal, 1);
			positions[5 * i + 3] = Vector3Utils.toVector4(axisA - axisB + normal, 1);
			positions[5 * i + 4] = Vector3Utils.toVector4(axisA + axisB + normal, 1);
		}
		GD.Print("T\\left(a,b,c,A,B,C\\right)=\\operatorname{triangle}\\left(P_{obj}\\left(a.x,a.y,A,B,C\\right),P_{obj}\\left(b.x,b.y,A,B,C\\right),P_{obj}\\left(c.x,c.y,A,B,C\\right)\\right)");
		GD.Print("P_{obj}\\left(p_{x},p_{y},A,B,C\\right)=Ap_{x}+Bp_{y}+C\\left(1-p_{x}-p_{y}\\right)");

		byte[] data = ToBytes<Vector4>(positions).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};

		uniform.AddId(buffer);
		_bindings.Add(uniform);

		return buffer;
	}

	private Rid CreateWriteList(int binding)
	{
		Vector4I[] writeList = new Vector4I[MaximumNodes];

		byte[] data = ToBytes<Vector4I>(writeList).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};

		uniform.AddId(buffer);
		_bindings.Add(uniform);

		return buffer;
	}


	private Rid CreateDataList(int binding)
	{
		Vector4[] dataList = new Vector4[30];

		byte[] data = ToBytes<Vector4>(dataList).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};

		uniform.AddId(buffer);
		_bindings.Add(uniform);

		return buffer;
	}

	private Rid CreateCameraData(int binding)
	{
		Transform3D transform = _camera.GlobalTransform;
		Basis basis = transform.Basis;
		Vector3 origin = transform.Origin;

		byte[] cameraData = ToBytes<float>(new float[]
		{
			basis.X.X, basis.X.Y, basis.X.Z, 1.0f,
			basis.Y.X, basis.Y.Y, basis.Y.Z, 1.0f,
			basis.Z.X, basis.Z.Y, basis.Z.Z, 1.0f,
			origin.X,  origin.Y,  origin.Z,  1.0f,
		    Mathf.DegToRad(_camera.Fov), _camera.Far, _camera.Near
		}).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)cameraData.Length, cameraData);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};

		uniform.AddId(buffer);
		_bindings.Add(uniform);

		return buffer;
	}

	private void UpdateCameraData(int binding)
	{
		Transform3D transform = _camera.GlobalTransform;
		Basis basis = transform.Basis;
		Vector3 origin = transform.Origin;

		byte[] cameraData = ToBytes<float>(new float[]
		{
				basis.X.X, basis.X.Y, basis.X.Z, 1.0f,
				basis.Y.X, basis.Y.Y, basis.Y.Z, 1.0f,
				basis.Z.X, basis.Z.Y, basis.Z.Z, 1.0f,
				origin.X,  origin.Y,  origin.Z,  1.0f,
				Mathf.DegToRad(_camera.Fov), _camera.Far, _camera.Near
		}).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)cameraData.Length, cameraData);
		RDUniform uniform = new()
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = binding
		};
		uniform.AddId(buffer);

		_bindings[binding] = uniform;
	}

	private void CreateUniforms()
	{
		_atomicCounterBuffer = CreateAtomicCounter(0);
		_indicesBlockBuffer = CreateIndicesBlock(1);
		_readList = CreateReadList(2);
		_writeList = CreateWriteList(3);
		_positions = CreatePositionList(4);
		_data = CreateDataList(5);
		_cameraData = CreateCameraData(6);
		_uniformSet = _rd.UniformSetCreate(_bindings, _shader, 0);
	}
	private void UpdateUniforms()
	{
		UpdateCameraData(6);
		_uniformSet = _rd.UniformSetCreate(_bindings, _shader, 0);
	}

	private void SetupComputeShader()
	{
		CreateRenderingDevice();
		CreateShader();
		CreatePipeline();
		CreateUniforms();
	}

	#endregion

	#region PROCESSING

	private async void StartProcessLoop()
	{
		int frq = 1000 / _updateFrequency;
		_processing = true;
		while (_processing)
		{
			Update();
			await Task.Delay(frq);
			Render();
		}
	}

	private void Update()
	{
		long computeList = _rd.ComputeListBegin();
		_rd.ComputeListBindComputePipeline(computeList, _pipeline);
		UpdateUniforms();
		_rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
		_rd.ComputeListDispatch(computeList, 4, 1, 1);
		_rd.ComputeListEnd();
		_rd.Submit();
	}
	int k;
	private void Render()
	{
		_rd.Sync();

		Vector4[] data = FromBytes<Vector4>(_rd.BufferGetData(_data)).ToArray();
		uint[] indices = FromBytes<uint>(_rd.BufferGetData(_indicesBlockBuffer)).ToArray();
		
		// CreateCameraData(6);
		// _uniformSet = _rd.UniformSetCreate(_bindings, _shader, 0);

		// GD.Print($"({data[1].X}, {data[1].Y}, {data[1].Z}), ({data[2].X}, {data[2].Y}, {data[2].Z}), ({data[3].X}, {data[3].Y}, {data[3].Z})");
		// GD.Print($"\n {data[4]}");
		// GD.Print($"\n {data[5]}");
		for (int i = 0; i< data.Length; i++) {
			GD.Print(data[i]);
		}
	}

	private void CleanupGPU()
	{
		if (_rd is null) return;
		_rd.FreeRid(_uniformSet);
		_rd.FreeRid(_pipeline);
		_rd.FreeRid(_shader);
		_rd.Free();
		_rd = null;
	}

	#endregion

	// Based discord user created these functions: idrmzit
	Span<byte> ToBytes<T>(Span<T> data) where T : unmanaged
	{
		return MemoryMarshal.Cast<T, byte>(data);
	}

	Span<T> FromBytes<T>(Span<byte> data) where T : unmanaged
	{
		// GD.Print(Unsafe.SizeOf<T>());
		int length = data.Length - (data.Length % Unsafe.SizeOf<T>());
		return MemoryMarshal.Cast<byte, T>(data[..length]);
	}
}
