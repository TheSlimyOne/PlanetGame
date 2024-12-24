using System;
using System.Linq;
using Uniform;
using Godot;
using Godot.Collections;
using Planet;
namespace Dispatcher
{
	public class RenderSurfaceDispatcher : ComputeShaderDispatcher<RenderSurfaceDispatcher.BufferNames>
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
			BASE_TRIANGLES,
			EXTERNAL_DATA,
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

				[BufferNames.BASE_TRIANGLES] = new StorageBufferUniform(this, _rd, (int)BufferNames.BASE_TRIANGLES,
					Utilities.ToBytes<Vector4>(PlanetData.GenerateTrianglePoints()).ToArray()
				),

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
					new MultimeshUniform.MultimeshParameters
					{
						Mesh = PlanetController.PlanetData.TriangleMesh.GetRid(),
						Scenario = PlanetController.SurfaceController.GetWorld3D().Scenario,
						Instance = RenderingServer.InstanceCreate(),
						InstanceCount = PlanetController.PlanetData.MaximumNodes,
						ExtraVisibilityMargin = 2 * PlanetController.PlanetData.Radius,
					},
					(int)BufferNames.MULTIMESH_BUFFER,
					false
				),
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
		}

		//TODO make this push-constants
		private byte[] GetExternalData()
		{
			Array<byte> data = new();
			data.AddRange(Utilities.ToBytesSingle(PlanetController.CameraController.GetViewProjectionMatrix()).ToArray());
			data.AddRange(Utilities.ToBytesSingle(Utilities.ToProjection(PlanetController.PlanetData.GetPlanetTransformMatrix())).ToArray());
			data.AddRange(Utilities.ToBytesSingle(VectorUtils.toVector4(PlanetController.CameraController.GlobalPosition, 0)).ToArray());
			data.AddRange(Utilities.ToBytes<float>(new float[]
			{
				Mathf.Tan(Mathf.DegToRad(PlanetController.CameraController.Fov) / 2),
				PlanetController.PlanetData.SubFactor,
				PlanetController.PlanetData.HeightScale,
				PlanetController.PlanetData.MaximumLOD,
				PlanetController.PlanetData.Radius,

				PlanetController.PlanetData.Bias1,
				PlanetController.PlanetData.Bias2,
				PlanetController.PlanetData.Culling ? 1 : 0,
				0
			}).ToArray());
			return data.ToArray();
		}

		// public void ComputePages()
		// {
		// 	_computeShaderUniforms[BufferNames.PAGING].UpdateUniform(
		// 		Utilities.ToBytesSingle(true).ToArray()
		// 	);
		// }

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
			return ((int)primCounts[indices[0]], (int)primCounts[indices[0] + 3]);
		}

		public int GetCurrentMaxLod()
		{
			return (int)GetUniform<Texture2DUniform>(BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R;
			
		}

	}
}