using System;
using Uniform;
using Godot;
using Godot.Collections;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;

namespace PlanetGame.Shaders.Dispatchers
{
	public class ExecuteTessellationPassDispatcher : Dispatcher<ExecuteTessellationPassDispatcher.BufferNames>
	{
		public PlanetController PlanetController { get; set; }
		public PrepareTessellationPassDispatcher PrepareTessellationPass { get; set; }
		public CustomCamera MainCamera { get; set; }
		public MultiMeshRD PlanetMultiMesh { get; set; }

		public SaveManager.WorldSave worldSave;

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

				[BufferNames.GLOBAL_KEYS_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.GLOBAL_KEYS_DATA,
                    GetInitialGlobalKeyData()
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

			VTData vTData = SaveManager.GetSVTData(worldSave);

			Array<byte> data =
			[
				.. Utilities.ToBytesSingle(vTData.LowResolutionMipCount),
				.. Utilities.ToBytesSingle(vTData.HighResolutionMipCount),
				.. Utilities.ToBytesSingle(vTData.TileSize),
				.. Utilities.ToBytes<int>(vTData.LodToMipMap),

				.. Utilities.ToBytesSingle(PlanetController.Radius),
				.. Utilities.ToBytesSingle(PlanetController.Resolution),
				.. Utilities.ToBytesSingle(PlanetController.Radius * PlanetController.HeightScale),
				.. Utilities.ToBytesSingle(PlanetController.SubFactor),
				.. Utilities.ToBytesSingle(debugFlags),

				.. Utilities.ToBytesSingle(PlanetController.MaximumLod),
				.. Utilities.ToBytesSingle(PlanetController.MinimumLod),


				.. Utilities.ToBytesSingle(PlanetController.MorphRange.X),
				.. Utilities.ToBytesSingle(PlanetController.MorphRange.Y),

				.. Utilities.ToBytesSingle(Utilities.ToProjection(PlanetController.GetPlanetTransform())),
				.. Utilities.ToBytesSingle(MainCamera.GetViewProjectionMatrix()),
				.. Utilities.ToBytesSingle(VectorUtils.ToVector4(MainCamera.GlobalPosition, Mathf.Tan(MainCamera.GetCameraFov(true) / 2))),
			];

			return [.. data];
		}

		private static byte[] GetInitialGlobalKeyData()
		{
			return [.. Utilities.ToBytes([
				3u,
				uint.MaxValue,
				0u,
				.. new uint[32]
			])];
		}

		public void ResetGlobalKeyData()
		{
			GetUniform<StorageBufferUniform>(BufferNames.GLOBAL_KEYS_DATA).UpdateUniform(GetInitialGlobalKeyData());
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

		public (int MaxLod, int MinLod, int StableCount, int[] LodCount) GetGlobalKeyData()
		{
			uint[] data = GetUniform<StorageBufferUniform>(BufferNames.GLOBAL_KEYS_DATA).GetData<uint>();

			int[] lodCount = new int[PlanetController.MaximumLod + 1];

			for (int i = PlanetController.MinimumLod; i <= PlanetController.MaximumLod; i++)
				lodCount[i] = (int)data[3 + i];

			return (
				(int)data[0],
				(int)data[1],
				(int)data[2],
				lodCount
			);
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