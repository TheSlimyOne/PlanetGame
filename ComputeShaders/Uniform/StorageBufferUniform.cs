using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlanetGame.ComputeShaders.Dispatcher;
using PlanetGame.Util;

namespace Uniform
{
	public partial class StorageBufferUniform : ComputeShaderUniform
	{
		public RenderingDevice.StorageBufferUsage StorageBufferUsage { get; private set; }

		public StorageBufferUniform(IDispatchable owner, RenderingDevice renderingDevice, int binding, byte[] data, RenderingDevice.StorageBufferUsage storageBufferUsage = 0, bool perserve = false) : base(renderingDevice, binding, owner, perserve)
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

		public StorageBufferUniform(IDispatchable owner, RenderingDevice renderingDevice, int binding, Rid rid, RenderingDevice.StorageBufferUsage storageBufferUsage = 0, bool perserve = false) : base(renderingDevice, binding, owner, perserve)
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

		private StorageBufferUniform(IDispatchable owner, StorageBufferUniform storageBufferUniform, int binding) : base(storageBufferUniform.RenderingDevice, binding, owner)
		{
			Rid = storageBufferUniform.Rid;
			StorageBufferUsage = storageBufferUniform.StorageBufferUsage;

			Uniform = new()
			{
				UniformType = RenderingDevice.UniformType.StorageBuffer,
				Binding = binding
			};

			foreach (Rid rid in storageBufferUniform.Uniform.GetIds())
			{
				Uniform.AddId(rid);
			}
		}

		public void ResizeBuffer(uint size)
		{
			Uniform.ClearIds();
			RenderingDevice.FreeRid(Rid);
			Rid = RenderingDevice.StorageBufferCreate(size, new byte[size], usage: StorageBufferUsage);
			Uniform.AddId(Rid);
		}

		public override StorageBufferUniform RebindUniform(IDispatchable owner, RenderingDevice rd, int binding)
		{
			if (rd == RenderingDevice)
				return new StorageBufferUniform(Owner, this, binding);
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

	}

}
