using System;
using System.Linq;
using ComputeShaderClasses;
using Godot;
using Godot.Collections;
namespace BufferClasses;

public partial class ComputeCullShader : ComputeShader<ComputeCullShader.BufferNames>
{   
    private PlanetController _planetController;
    private ComputeCopyShader _computeCopyShader;

    public enum BufferNames
	{
		ATOMIC_COUNTER,
		INDICES,
		READ_LIST,
		GLOBAL_KEYS_DATA,
		WRITE_FULL_LIST,
		WRITE_CULLED_LIST,
		TRIANGLE_COORDINATES,
		EXTERNAL_DATA,
		DEBUG_DATA,
		HEIGHT_MAP,
		HEIGHT_GRADIENT,
		KEYS,
	}


    public ComputeCullShader(string shaderFilePath, PlanetController planetController, RenderingDevice rd = null) : base(shaderFilePath, rd)
    {
        _planetController = planetController;
        SetupComputeShader();
    }
    public void SetComputeCopyShader(ComputeCopyShader computeCopyShader)
    {
        _computeCopyShader = computeCopyShader;
    }

    public override void CreateUniforms()
    {
       _computeShaderUniforms = new System.Collections.Generic.Dictionary<BufferNames, ComputeShaderUniform>()
		{
			// Full      list  0  - 15
			// Culling   list  16 - 31
			[BufferNames.ATOMIC_COUNTER] = new StorageBufferUniform(_rd, (int)BufferNames.ATOMIC_COUNTER,
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
			[BufferNames.INDICES] = new StorageBufferUniform(_rd, (int)BufferNames.INDICES,
				Utilities.ToBytes<uint>(new uint[] { 0, 1, 8, (uint)_planetController.PlanetData.MaximumNodes }).ToArray()
			),

			// key = uvec4(nodeIDMSB, nodeIDLSB, meshPolygonID, flagsAndRootID)
			[BufferNames.READ_LIST] = new StorageBufferUniform(_rd, (int)BufferNames.READ_LIST,
				new Func<byte[]>(() =>
				{
					Key[] readList = new Key[_planetController.PlanetData.MaximumNodes];

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

			[BufferNames.WRITE_FULL_LIST] = new StorageBufferUniform(_rd, (int)BufferNames.WRITE_FULL_LIST,
				Utilities.ToBytes<Key>(new Key[_planetController.PlanetData.MaximumNodes]).ToArray()
			),

			[BufferNames.WRITE_CULLED_LIST] = new StorageBufferUniform(_rd, (int)BufferNames.WRITE_CULLED_LIST,
				Utilities.ToBytes<Key>(new Key[_planetController.PlanetData.MaximumNodes]).ToArray()
			),

			[BufferNames.TRIANGLE_COORDINATES] = new StorageBufferUniform(_rd, (int)BufferNames.TRIANGLE_COORDINATES,
				Utilities.ToBytes<Vector4>(_planetController.PlanetData.GenerateTrianglePoints()).ToArray()
			),

			[BufferNames.DEBUG_DATA] = new StorageBufferUniform(_rd, (int)BufferNames.DEBUG_DATA,
				new Func<byte[]>(() =>
				{
					return Utilities.ToBytes<bool>(new bool[] { Engine.IsEditorHint() }).ToArray();
				}).Invoke()
			),

			[BufferNames.HEIGHT_MAP] = new Texture2DUniform(_rd, (int)BufferNames.HEIGHT_MAP,
				_planetController.PlanetData.HeightMap),

			[BufferNames.HEIGHT_GRADIENT] = new Texture2DUniform(_rd, (int)BufferNames.HEIGHT_GRADIENT,
				_planetController.PlanetData.HeightGradient),

			[BufferNames.KEYS] = new Texture2DUniform(_rd, (int)BufferNames.KEYS,
				new RDTextureFormat()
				{
					Width = (uint)(Mathf.Sqrt(_planetController.PlanetData.MaximumNodes) * 1f / 2f),
					Height = (uint)(Mathf.Sqrt(_planetController.PlanetData.MaximumNodes) * 1f / 2f),
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

			[BufferNames.GLOBAL_KEYS_DATA] = new Texture2DUniform(_rd, (int)BufferNames.GLOBAL_KEYS_DATA,
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

			[BufferNames.EXTERNAL_DATA] = new StorageBufferUniform(_rd, (int)BufferNames.EXTERNAL_DATA,
				GetExternalData()
			),
		};

        foreach(ComputeShaderUniform computeShaderUniform in _computeShaderUniforms.Values)
        {
            _bindings.Add(computeShaderUniform.Uniform);
        }

        _uniformSet = _rd.UniformSetCreate(_bindings, _shader, 0);
    }

    public override void Run()
    {
		long computeList = _rd.ComputeListBegin();
		_rd.ComputeListBindComputePipeline(computeList, _pipeline);
		_rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
		_rd.ComputeListDispatchIndirect(computeList, _computeCopyShader.GetUniformRid(ComputeCopyShader.BufferNames.DISPATCH_BUFFER), 0);
		_rd.ComputeListEnd();
		SubmitThenSync();
    }

    public override void UpdateUniforms()
    {
        _computeShaderUniforms[BufferNames.INDICES].UpdateUniform(
			GetIndicesData()
		);
		_computeShaderUniforms[BufferNames.READ_LIST].UpdateUniform(
            _computeShaderUniforms[BufferNames.WRITE_FULL_LIST].GetByteData()
		);

		_computeShaderUniforms[BufferNames.EXTERNAL_DATA].UpdateUniform(
			GetExternalData()
		);
    }

    private byte[] GetIndicesData()
	{
		uint[] indices = _computeShaderUniforms[BufferNames.INDICES].GetData<uint>();
		indices[0] = (indices[0] + 1) % 16; // Read Index
		indices[1] = (indices[1] + 1) % 16; // Write Index
		indices[2] = (indices[2] + 1) % 16; // Delete Index
		indices[3] = (uint)_planetController.PlanetData.MaximumNodes;
		return Utilities.ToBytes<uint>(indices).ToArray();
	}

    private byte[] GetExternalData()
	{
		Array<byte> data = new();
		
		data.AddRange(Utilities.ToBytesSingle(_planetController.CameraController.GetViewProjectionMatrix()).ToArray());
		data.AddRange(Utilities.ToBytesSingle(VectorUtils.toVector4(_planetController.CameraController.GlobalPosition, 0)).ToArray());
		data.AddRange(Utilities.ToBytesSingle(Utilities.ToProjection(_planetController.PlanetData.GetPlanetTransformMatrix())).ToArray());
		data.AddRange(Utilities.ToBytes<float>(new float[]
		{
			Mathf.DegToRad(_planetController.CameraController.Fov),
			_planetController.PlanetData.SubFactor * _planetController.PlanetData.Radius,
            _planetController.PlanetData.MorphFactor,
            0
		}).ToArray());
		return data.ToArray();
	}
}