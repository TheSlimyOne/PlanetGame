using System;
using Uniform;
using Godot;
using Godot.Collections;
using PlanetGame.Util;

namespace PlanetGame.Shaders.Dispatchers
{
	public class ExecuteTessellationPassDispatcher : Dispatcher<ExecuteTessellationPassDispatcher.BufferNames>
	{
		public PlanetController PlanetController { get; set; }
		public PrepareTessellationPassDispatcher PrepareTessellationPass { get; set; }
		public CustomCamera MainCamera { get; set; }
		public CustomCamera HelperCamera { get; set; }
		public MultiMeshRD PlanetMultiMesh { get; set; }

		public enum BufferNames
		{
			ATOMIC_COUNTER,
			KEY_INDICES,
			READ_LIST,
			WRITE_FULL_LIST,
			WRITE_CULL_LIST,
			EXTERNAL_DATA,
			MULTIMESH_BUFFER,
			GLOBAL_KEYS_DATA,
		}

		public ExecuteTessellationPassDispatcher() : base(new() { Compute = ShaderPaths.EXECUTE_TESSELLATION_PASS })
		{
			SetupShader();
		}

		public override void CreateUniforms()
		{
			_computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ShaderUniform>()
			{
				// Full      list  0 - 2
				// Culling   list  3 - 5
				// Rendered  list  6 - 8
				[BufferNames.ATOMIC_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.ATOMIC_COUNTER,
					new Func<byte[]>(() =>
					{
						uint[] primCounts = new uint[3 * 3];
						primCounts[0] = 6 * (uint)Mathf.Pow(4, PlanetController.StartingLod + 1);
						// GD.PrintS(PlanetController.StartingLod + 1, PlanetController.MaximumLod, PlanetController.MinimumLod);
						return Utilities.ToBytes<uint>(primCounts).ToArray();
					}).Invoke()
				),

				// 0 Read Index
				// 1 Write Index
				// 2 Delete Index
				// 3 Max nodes
				[BufferNames.KEY_INDICES] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.KEY_INDICES,
					Utilities.ToBytes<uint>([0, 1, 2, (uint)PlanetController.MaximumKeys]).ToArray()
				),

				// key = uvec4(nodeIdMSB, nodeIdLSB, meshPolygonId, flagsAndRootId)
				[BufferNames.READ_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.READ_LIST,
				// Utilities.ToBytes<Key>(readList).ToArray()
				new Func<byte[]>(() =>
				{
					Key[] readList = new Key[PlanetController.MaximumKeys];

					for (int i = 0; i < 6; i++)
					{
						Key[] faceData = Key.GenerateFullFace(PlanetController.StartingLod, i);
						// GD.PrintS(PlanetController.StartingLod, faceData.Length);
						System.Array.Copy(faceData, 0, readList, i * faceData.Length, faceData.Length);
					}
					return Utilities.ToBytes<Key>(readList).ToArray();
				}).Invoke()
				),

				[BufferNames.WRITE_FULL_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.WRITE_FULL_LIST,
					Utilities.ToBytes<Key>(new Key[PlanetController.MaximumKeys]).ToArray()
				),

				[BufferNames.WRITE_CULL_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.WRITE_CULL_LIST,
					Utilities.ToBytes<Key>(new Key[PlanetController.MaximumKeys]).ToArray()
				),

				[BufferNames.EXTERNAL_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.EXTERNAL_DATA,
					GetExternalData()
				),

				[BufferNames.MULTIMESH_BUFFER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.MULTIMESH_BUFFER,
					PlanetMultiMesh.Buffer, perserve: true
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
			};
			CreateUniformSet();
		}

		#nullable enable
        public override void Invoke(byte[]? pushConstants = null)
		{
			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
			RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatchIndirect(computeList, PrepareTessellationPass[PrepareTessellationPassDispatcher.BufferNames.EXEC_DISPATCH_BUFFER].Rid, 0);
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
			uint debugFlags = Utilities.ToBitFlags([
				PlanetController.IsCulling,
				PlanetController.IsMorphing,
				PlanetController.IsCube,
			]);

			Array<byte> data =
			[
				.. Utilities.ToBytesSingle(SaveManager.GetCurrentSave().TileSize),
				.. Utilities.ToBytesSingle(SaveManager.GetCurrentSave().TotalLods),
				.. Utilities.ToBytes<int>(SaveManager.GetCurrentSave().LodToMipMap),

				.. Utilities.ToBytesSingle(PlanetController.Radius),
				.. Utilities.ToBytesSingle(PlanetController.Resolution),
				.. Utilities.ToBytesSingle(PlanetController.HeightScale),
				.. Utilities.ToBytesSingle(PlanetController.SubFactor),
				.. Utilities.ToBytesSingle(debugFlags),

				.. Utilities.ToBytesSingle(PlanetController.MaximumLod),
				.. Utilities.ToBytesSingle(PlanetController.MinimumLod),

				.. Utilities.ToBytesSingle(0),

				.. Utilities.ToBytesSingle(PlanetController.MorphRange.X),
				.. Utilities.ToBytesSingle(PlanetController.MorphRange.Y),

				.. Utilities.ToBytesSingle(Utilities.ToProjection(PlanetController.GetPlanetTransformMatrix())),
				.. Utilities.ToBytesSingle(HelperCamera.GetViewProjectionMatrix()),
				.. Utilities.ToBytesSingle(VectorUtils.ToVector4( MainCamera.GlobalPosition, Mathf.Tan(HelperCamera.GetCameraFov(true) / 2))),
			];

			return [.. data];
		}

		public Key[] GetKeys()
		{
			(int all, _, _) = GetPrimitiveCounts();
			return GetUniform<StorageBufferUniform>(BufferNames.WRITE_FULL_LIST).GetData<Key>(sizeBytes: (uint)all * Utilities.SizeOf<Key>());
		}
		public Key[] GetReadList()
		{
			return GetUniform<StorageBufferUniform>(BufferNames.READ_LIST).GetData<Key>(sizeBytes: 96u * Utilities.SizeOf<Key>());
		}

		public void ResizeReadList()
		{
			throw new NotImplementedException();
		}
		public void ResizeWriteList()
		{
			throw new NotImplementedException();
		}

		public (int fullPrimCount, int culledPrimCount, int renderedPrimCount) GetPrimitiveCounts()
		{
			uint[] indices = GetUniform<StorageBufferUniform>(BufferNames.KEY_INDICES).GetData<uint>();
			uint[] primCounts = GetUniform<StorageBufferUniform>(BufferNames.ATOMIC_COUNTER).GetData<uint>();

			return ((int)primCounts[indices[0]], (int)primCounts[indices[0] + 3], (int)primCounts[indices[0] + 6]);
		}

		public int GetCurrentLod()
		{
			return (int)GetUniform<Texture2DUniform>(BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R - 1;
		}

		public void ClearGlobalKeyData()
		{
			GetUniform<Texture2DUniform>(BufferNames.GLOBAL_KEYS_DATA).ClearTexture(Colors.Black);
		}

		// TODO maybe finish this
		public void GetMultimeshBuffer()
		{
			float[] data = GetUniform<StorageBufferUniform>(BufferNames.MULTIMESH_BUFFER).GetData<float>();
			int instanceCount = PrepareTessellationPass.GetUniform<StorageBufferUniform>(PrepareTessellationPassDispatcher.BufferNames.MULTIMESH_COMMAND_BUFFER).GetData<int>()[1];

			for (int i = 0; i < instanceCount; i++)
			{
				int baseIndex = i * 20;
				Key key = new(data[baseIndex + 16], data[baseIndex + 17], data[baseIndex + 18], data[baseIndex + 19]);
			}
		}
	}
}