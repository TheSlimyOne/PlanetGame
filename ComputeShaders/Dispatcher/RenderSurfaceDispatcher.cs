using System;
using System.Linq;
using Uniform;
using Godot;
using Godot.Collections;
using Planet;
namespace Dispatcher
{
	public partial class RenderSurfaceDispatcher : ComputeShaderDispatcher<RenderSurfaceDispatcher.BufferNames>
	{
		public PlanetController PlanetController { get; set; }
		public CopyKeysDispatcher CopyKeysDispatcher { get; set; }

		public enum BufferNames
		{
			ATOMIC_COUNTER,
			INDICES,
			READ_LIST,
			GLOBAL_KEYS_DATA,
			WRITE_FULL_LIST,
			TRIANGLE_COORDINATES,
			EXTERNAL_DATA,
			DEBUG_DATA,
			HEIGHT_MAP,
			MULTIMESH_BUFFER,
		}

		public RenderSurfaceDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
		{
			SetupComputeShader();
		}

		public override void CreateUniforms()
		{
			_computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
			{
				// Full      list  0 - 2
				// Culling   list  3 - 6
				[BufferNames.ATOMIC_COUNTER] = new StorageBufferUniform(this, _rd, (int)BufferNames.ATOMIC_COUNTER,
					new Func<byte[]>(() =>
					{
						uint[] primCounts = new uint[2 * 3];
						primCounts[0] = 6 * (uint)Mathf.Pow(4, PlanetController.PlanetData.StartingLod + 1);
						return Utilities.ToBytes<uint>(primCounts).ToArray();
					}).Invoke()
				 ),

				// 0 Read Index
				// 1 Write Index
				// 2 Delete Index
				// 3 Max nodes
				[BufferNames.INDICES] = new StorageBufferUniform(this, _rd, (int)BufferNames.INDICES,
					Utilities.ToBytes<uint>(new uint[] { 0, 1, 2, (uint)PlanetController.PlanetData.MaximumNodes }).ToArray()
				),

				// key = uvec4(nodeIDMSB, nodeIDLSB, meshPolygonID, flagsAndRootID)
				[BufferNames.READ_LIST] = new StorageBufferUniform(this, _rd, (int)BufferNames.READ_LIST,
				new Func<byte[]>(() =>
				{
					Key[] readList = new Key[PlanetController.PlanetData.MaximumNodes];

					for (int i = 0; i < 6; i++)
					{
						Key[] faceData = Key.GenerateFullFace(PlanetController.PlanetData.StartingLod, i);
						System.Array.Copy(faceData, 0, readList, i * faceData.Length, faceData.Length);
					}

					return Utilities.ToBytes<Key>(readList).ToArray();
				}).Invoke()
				),

				[BufferNames.WRITE_FULL_LIST] = new StorageBufferUniform(this, _rd, (int)BufferNames.WRITE_FULL_LIST,
					Utilities.ToBytes<Key>(new Key[PlanetController.PlanetData.MaximumNodes]).ToArray()
				),

				[BufferNames.TRIANGLE_COORDINATES] = new StorageBufferUniform(this, _rd, (int)BufferNames.TRIANGLE_COORDINATES,
					Utilities.ToBytes<Vector4>(PlanetData.GenerateTrianglePoints()).ToArray()
				),

				[BufferNames.DEBUG_DATA] = new StorageBufferUniform(this, _rd, (int)BufferNames.DEBUG_DATA,
					new Func<byte[]>(() =>
					{
						return Utilities.ToBytes<bool>(new bool[] { PlanetController.PlanetData.Culling }).ToArray();
					}).Invoke()
				 ),

				[BufferNames.HEIGHT_MAP] = new Func<Texture2DUniform>(() =>
				{
					Image image = PlanetController.PlanetData.HeightMap.GetImage();
					image.ClearMipmaps();
					image.Convert(Image.Format.L8);

					return new Texture2DUniform(this, _rd, (int)BufferNames.HEIGHT_MAP,
						new RDTextureFormat()
						{
							Width = (uint)image.GetWidth(),
							Height = (uint)image.GetHeight(),
							TextureType = RenderingDevice.TextureType.Type2D,
							Format = RenderingDevice.DataFormat.R8Unorm,
							UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
						}, RenderingDevice.UniformType.SamplerWithTexture, textureData: new() { image.GetData() });
				}).Invoke(),

				[BufferNames.GLOBAL_KEYS_DATA] = new Texture2DUniform(this, _rd, (int)BufferNames.GLOBAL_KEYS_DATA,
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
					}, RenderingDevice.UniformType.Image
				),

				[BufferNames.EXTERNAL_DATA] = new StorageBufferUniform(this, _rd, (int)BufferNames.EXTERNAL_DATA,
					GetExternalData()
				),

				[BufferNames.MULTIMESH_BUFFER] = new MultimeshUniform(this,
					new MultimeshUniform.MultimeshParameters(
						PlanetController.PlanetData.TriangleMesh.GetRid(),
						PlanetController.SurfaceController.GetWorld3D().Scenario,
						RenderingServer.InstanceCreate(),
						PlanetController.PlanetData.MaximumNodes,
						2 * PlanetController.PlanetData.Radius
						),
					(int)BufferNames.MULTIMESH_BUFFER,
					false
				)
			};

			CreateUniformSet();
		}

		public override void Ready()
		{
			long computeList = _rd.ComputeListBegin();
			_rd.ComputeListBindComputePipeline(computeList, _pipeline);
			_rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			_rd.ComputeListAddBarrier(computeList);
			_rd.ComputeListDispatchIndirect(computeList, CopyKeysDispatcher.GetUniformRid(CopyKeysDispatcher.BufferNames.DISPATCH_BUFFER), 0);
			_rd.ComputeListEnd();
		}

		public override void UpdateUniforms()
		{
			_computeShaderUniforms[BufferNames.READ_LIST].UpdateUniform(
				_computeShaderUniforms[BufferNames.WRITE_FULL_LIST].GetByteData()[0]
			);

			_computeShaderUniforms[BufferNames.EXTERNAL_DATA].UpdateUniform(
				GetExternalData()
			);

			_computeShaderUniforms[BufferNames.DEBUG_DATA].UpdateUniform(
				Utilities.ToBytesSingle(PlanetController.PlanetData.Culling).ToArray()
			);

			_computeShaderUniforms[BufferNames.INDICES].UpdateUniform(
				GetIndicesData()
			);
		}

		private byte[] GetExternalData()
		{
			Array<byte> data = new();

			data.AddRange(Utilities.ToBytesSingle(PlanetController.CameraController.GetViewProjectionMatrix()).ToArray());
			data.AddRange(Utilities.ToBytesSingle(VectorUtils.toVector4(PlanetController.CameraController.GlobalPosition, 0)).ToArray());
			data.AddRange(Utilities.ToBytesSingle(Utilities.ToProjection(PlanetController.PlanetData.GetPlanetTransformMatrix())).ToArray());
			data.AddRange(Utilities.ToBytes<float>(new float[]
			{
				Mathf.Tan(Mathf.DegToRad(PlanetController.CameraController.Fov) / 2),
				PlanetController.PlanetData.SubFactor * PlanetController.PlanetData.Radius,
				PlanetController.PlanetData.HeightScale,
				PlanetController.PlanetData.MaximumLOD,
				PlanetController.PlanetData.Radius
			}).ToArray());
			return data.ToArray();
		}

		private byte[] GetIndicesData()
		{
			uint[] indices = ((StorageBufferUniform)_computeShaderUniforms[BufferNames.INDICES]).GetData<uint>();
			indices[0] = (indices[0] + 1) % 3; // Read Index
			indices[1] = (indices[1] + 1) % 3; // Write Index
			indices[2] = (indices[2] + 1) % 3; // Delete Index
			indices[3] = (uint)PlanetController.PlanetData.MaximumNodes;
			return Utilities.ToBytes<uint>(indices).ToArray();
		}


		public void ResizeReadList()
		{
			throw new NotImplementedException();
		}
		public void ResizeWriteList()
		{
			throw new NotImplementedException();
		}

		public (int, int) GetPrimitiveCounts()
		{
			uint[] indices = GetUniformData<uint>(BufferNames.INDICES);
			uint[] primCounts = GetUniformData<uint>(BufferNames.ATOMIC_COUNTER);
			GD.Print("==================================");
			GD.PrintS("FULL", primCounts[0], primCounts[1], primCounts[2]);
			GD.PrintS("CULL", primCounts[3], primCounts[4], primCounts[5]);
			GD.Print(indices[3]);
			return ((int)primCounts[indices[1]], (int)primCounts[indices[1] + 3]);
		}
	}
}