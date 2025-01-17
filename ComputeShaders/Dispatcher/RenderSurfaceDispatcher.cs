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
		public PlanetData PlanetData { get; set; }
		public CopyKeysDispatcher CopyKeysDispatcher { get; set; }
		public CustomCamera MainCamera { get; set; }
		public CustomCamera HelperCamera { get; set; }

		public enum BufferNames
		{
			ATOMIC_COUNTER,
			INDICES,
			READ_LIST,
			GLOBAL_KEYS_DATA,
			WRITE_FULL_LIST,
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
				[BufferNames.ATOMIC_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.ATOMIC_COUNTER,
					new Func<byte[]>(() =>
					{
						uint[] primCounts = new uint[2 * 3];
						primCounts[0] = 6 * (uint)Mathf.Pow(4, PlanetData.StartingLod + 1);
						return Utilities.ToBytes<uint>(primCounts).ToArray();
					}).Invoke()
				 ),

				// 0 Read Index
				// 1 Write Index
				// 2 Delete Index
				// 3 Max nodes
				[BufferNames.INDICES] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.INDICES,
					Utilities.ToBytes<uint>(new uint[] { 0, 1, 2, (uint)PlanetData.MaximumNodes }).ToArray()
				),

				// key = uvec4(nodeIDMSB, nodeIDLSB, meshPolygonID, flagsAndRootID)
				[BufferNames.READ_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.READ_LIST,
				new Func<byte[]>(() =>
				{
					Key[] readList = new Key[PlanetData.MaximumNodes];
					
					for (int i = 0; i < 6; i++)
					{
						Key[] faceData = Key.GenerateFullFace(PlanetData.StartingLod, i);
						System.Array.Copy(faceData, 0, readList, i * faceData.Length, faceData.Length);
					}
					return Utilities.ToBytes<Key>(readList).ToArray();
				}).Invoke()
				),

				[BufferNames.WRITE_FULL_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.WRITE_FULL_LIST,
					Utilities.ToBytes<Key>(new Key[PlanetData.MaximumNodes]).ToArray()
				),

				[BufferNames.GLOBAL_KEYS_DATA] = new Texture2DUniform(this, RenderingDevice, (int)BufferNames.GLOBAL_KEYS_DATA,
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

				[BufferNames.EXTERNAL_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.EXTERNAL_DATA,
					GetExternalData()
				),

				[BufferNames.MULTIMESH_BUFFER] = new MultimeshUniform(this,
					(int)BufferNames.MULTIMESH_BUFFER,
					PlanetData.MaximumNodes,
					PlanetData.TriangleMesh.GetRid(),
					-1
				),
			};
			CreateUniformSet();
		}

		public override void Ready()
		{
			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
			RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatchIndirect(computeList, CopyKeysDispatcher.GetUniformRid(CopyKeysDispatcher.BufferNames.DISPATCH_BUFFER), 0);
			RenderingDevice.ComputeListEnd();
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
			Array<byte> data =
            [
                .. Utilities.ToBytesSingle(HelperCamera.GetViewProjectionMatrix()),
                .. Utilities.ToBytesSingle(Utilities.ToProjection(PlanetData.GetPlanetTransformMatrix())),
                .. Utilities.ToBytesSingle(VectorUtils.ToVector4(MainCamera.GlobalPosition, 0)),
                .. Utilities.ToBytes(
                [
                    Mathf.Tan(HelperCamera.GetCameraFov(true) / 2),
                    PlanetData.SubFactor,
                    PlanetData.HeightScale,
                    PlanetData.MaximumLOD,
                    PlanetData.Radius,

                    PlanetData.Bias1,
                    PlanetData.Bias2,
                    PlanetData.Culling ? 1 : 0,
                    0
                ]),
            ];
			return [.. data];
		}

		// public void ComputePages()
		// {
		// 	_computeShaderUniforms[BufferNames.PAGING].UpdateUniform(
		// 		Utilities.ToBytesSingle(true).ToArray()
		// 	);
		// }

		public Rid CreateMultimeshInstance(Transform3D transform, Rid senario, float extraVisibilityMargin, uint layerMask)
		{
			return GetUniform<MultimeshUniform>(BufferNames.MULTIMESH_BUFFER).CreateMultimeshInstance(
				transform, senario, extraVisibilityMargin, layerMask
			);
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
			return ((int)primCounts[indices[0]], (int)primCounts[indices[0] + 3]);
		}

		public int GetCurrentLod()
		{
			return (int)GetUniform<Texture2DUniform>(BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R;
		}
    }
}