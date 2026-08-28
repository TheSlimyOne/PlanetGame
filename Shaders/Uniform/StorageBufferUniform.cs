using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlanetGame.Shaders;
using PlanetGame.Shaders.Dispatchers;
using PlanetGame.Util;

namespace Uniform
{
	public partial class StorageBufferUniform : ShaderUniform
	{
		public RenderingDevice.StorageBufferUsage StorageBufferUsage { get; private set; }

		public StorageBufferUniform(IGPUResource owner, RenderingDevice renderingDevice, int binding, byte[] data, RenderingDevice.StorageBufferUsage storageBufferUsage = 0, bool perserve = false) : base(renderingDevice, binding, owner, perserve)
		{
			Rid = renderingDevice.StorageBufferCreate((uint)data.Length, data, usage: storageBufferUsage);
			StorageBufferUsage = storageBufferUsage;

			Uniform = new()
			{
				UniformType = RenderingDevice.UniformType.StorageBuffer,
				Binding = binding
			};
			Uniform.AddId(Rid);
		}

		public StorageBufferUniform(IGPUResource owner, RenderingDevice renderingDevice, int binding, Rid rid, RenderingDevice.StorageBufferUsage storageBufferUsage = 0, bool perserve = false) : base(renderingDevice, binding, owner, perserve)
		{
			Rid = rid;
			StorageBufferUsage = storageBufferUsage;

			Uniform = new()
			{
				UniformType = RenderingDevice.UniformType.StorageBuffer,
				Binding = binding
			};
			Uniform.AddId(Rid);
		}

		private StorageBufferUniform(IGPUResource owner, StorageBufferUniform storageBufferUniform, int binding) : base(storageBufferUniform.RenderingDevice, binding, owner, storageBufferUniform.Perserved)
		{
			Rid = storageBufferUniform.Rid;
			StorageBufferUsage = storageBufferUniform.StorageBufferUsage;

			Uniform = new()
			{
				UniformType = RenderingDevice.UniformType.StorageBuffer,
				Binding = binding
			};

			foreach (Rid rid in storageBufferUniform.Uniform.GetIds())
				Uniform.AddId(rid);
		}

		public void ResizeBuffer(uint size)
		{
			RenderingDevice.FreeRid(Rid);
			SetRid(RenderingDevice.StorageBufferCreate(size, new byte[size], usage: StorageBufferUsage));
		}

		public override StorageBufferUniform RebindUniform(IGPUResource owner, RenderingDevice rd, int binding)
		{
			if (rd == RenderingDevice)
				return new StorageBufferUniform(owner, this, binding);
			else
				return new StorageBufferUniform(owner, rd, binding, GetByteData()[0], storageBufferUsage: StorageBufferUsage);
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

		public void SetRid(Rid rid)
        {
            Rid = rid;

            Uniform.ClearIds();
            Uniform.AddId(Rid);
        }
	}
}
