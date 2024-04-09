using Godot;
using Godot.Collections;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;

public partial class Dispatcher : Node
{
	[ExportGroup("Settings")]
	[Export(PropertyHint.Range, "1, 1000")] private int _updateFrequency = 60;
	[Export] private bool _autoStart;

	[ExportGroup("Requirements")]
	[Export(PropertyHint.File)] private string _computeShader;

	public int MaximumNodes = 32;

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

	private bool _processing;

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
		uint data_size = 2 * 96 * sizeof(uint);
		Rid buffer = _rd.StorageBufferCreate(data_size, new byte[data_size]);
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
		uint data_size = 2 * sizeof(int);
		Rid buffer = _rd.StorageBufferCreate(data_size, new byte[data_size]);
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

		for (int i = 0; i < 1; i++)
		{
			readList[i + 0] = new Vector4I(0, 1, i, 0);
			readList[i + 1] = new Vector4I(0, 1, i, 1);
			readList[i + 2] = new Vector4I(0, 1, i, 2);
			readList[i + 3] = new Vector4I(0, 1, i, 3);
		}

		byte[] data = ToBytes<Vector4I>(readList).ToArray();

		Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data);
;
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


	private void CreateUniforms()
	{
		_atomicCounterBuffer = CreateAtomicCounter(0);
		_indicesBlockBuffer = CreateIndicesBlock(1);
		_readList = CreateReadList(2);
		_writeList = CreateWriteList(3);
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
		_rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
		_rd.ComputeListDispatch(computeList, 24, 1, 1);
		_rd.ComputeListEnd();
		_rd.Submit();
	}
	int k;
	private void Render()
	{
		_rd.Sync();
		if (k++ > 0)
			return;

		byte[] bytes = _rd.BufferGetData(_writeList);
		Vector4I[] data = FromBytes<Vector4I>(bytes).ToArray();



		for (int i = 0; i < data.Length; i++)
		{
			GD.PrintS(i, data[i]);
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
	Span<byte> ToBytes<T>(Span<T> data) where T : unmanaged => MemoryMarshal.Cast<T, byte>(data);

	Span<T> FromBytes<T>(Span<byte> data) where T : unmanaged
	{
		int length = data.Length - (data.Length % Unsafe.SizeOf<T>());
		return MemoryMarshal.Cast<byte, T>(data[..length]);
	}
}
