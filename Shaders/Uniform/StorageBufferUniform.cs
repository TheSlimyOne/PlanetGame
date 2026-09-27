using System.Collections.Generic;
using Godot;
using PlanetGame.Shaders;
using PlanetGame.Util;

namespace Uniform
{
	public class StorageBufferUniform : ShaderUniform
	{
		public RenderingDevice.StorageBufferUsage StorageBufferUsage { get; private set; }

		public void SetRid(Rid rid, bool perserved = true, RenderingDevice.StorageBufferUsage storageBufferUsage = 0, bool freePreviousRid = false)
		{
			if (Rid != rid && freePreviousRid && !Perserved && Rid.IsValid)
				RenderingDevice.FreeRid(Rid);

			Rid = rid;
			StorageBufferUsage = storageBufferUsage;
			Perserved = perserved;
		}

		public StorageBufferUniform(IGPUResource owner, RenderingDevice renderingDevice, byte[] data, RenderingDevice.StorageBufferUsage storageBufferUsage = 0, bool perserved = false) : base(renderingDevice, owner, perserved)
		{
			Rid = renderingDevice.StorageBufferCreate((uint)data.Length, data, usage: storageBufferUsage);
			UniformType = RenderingDevice.UniformType.StorageBuffer;
			StorageBufferUsage = storageBufferUsage;
		}

		public StorageBufferUniform(IGPUResource owner, RenderingDevice renderingDevice, RenderingDevice.StorageBufferUsage storageBufferUsage = 0, bool perserved = false) : base(renderingDevice, owner, perserved)
		{
			Rid = new();
			UniformType = RenderingDevice.UniformType.StorageBuffer;
			StorageBufferUsage = storageBufferUsage;
		}

		public override RDUniform CreateRDUniform(int binding)
		{
			RDUniform uniform = new()
			{
				UniformType = UniformType,
				Binding = binding
			};
			
			uniform.AddId(Rid);
			return uniform;
		}

		public T[] GetData<T>(uint offsetBytes = 0, uint sizeBytes = 0) where T : unmanaged => Utilities.FromBytes<T>(RenderingDevice.BufferGetData(Rid, offsetBytes, sizeBytes)).ToArray();

		public Error GetDataAsync(Callable callback, uint offsetBytes = 0, uint sizeBytes = 0) => RenderingDevice.BufferGetDataAsync(Rid, callback, offsetBytes, sizeBytes);

		public override void UpdateUniform(byte[] data)
		{
			RenderingDevice.BufferUpdate(Rid, 0, (uint)data.Length, data);
		}

		public void UpdateUniform(uint offset, uint sizeBytes, byte[] data)
		{
			RenderingDevice.BufferUpdate(Rid, offset, sizeBytes, data);
		}

		public override List<byte[]> GetByteData() => [RenderingDevice.BufferGetData(Rid)];
	}
}
