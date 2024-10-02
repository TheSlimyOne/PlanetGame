using System;
using Godot;
using Godot.Collections;
using Dispatcher;

namespace Uniform
{
    public partial class StorageBufferUniform : ComputeShaderUniform
    {
        public int Indirect { get; private set; }

        public StorageBufferUniform(IDispatchable owner, RenderingDevice renderingDevice, int binding, byte[] data, int indirect = 0) : base(renderingDevice, binding, owner)
        {
            Rid = renderingDevice.StorageBufferCreate((uint)data.Length, data, usage: (RenderingDevice.StorageBufferUsage)indirect);
            Indirect = indirect;

            Uniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = binding
            };
            Uniform.AddId(Rid);
        }

        public StorageBufferUniform(IDispatchable owner, StorageBufferUniform storageBufferUniform, int binding) : base(storageBufferUniform._rd, binding, owner)
        {
            Rid = storageBufferUniform.Rid;
            Indirect = storageBufferUniform.Indirect;

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

        public override StorageBufferUniform RebindUniform(IDispatchable owner, RenderingDevice rd, int binding) 
        {
            if (rd == _rd)
                return new StorageBufferUniform(owner, this, binding);
            else
                return new StorageBufferUniform(owner, rd, binding, GetByteData()[0], indirect: Indirect);
        }

        public T[] GetData<T>() where T : unmanaged => Utilities.FromBytes<T>(_rd.BufferGetData(Rid)).ToArray();


        public override void UpdateUniform(byte[] data)
        {
            _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);
        }

        public override Array<byte[]> GetByteData() => new() { _rd.BufferGetData(Rid) };
        // public override Array<byte[]> GetByteData(uint offsetBytes = 0, uint sizeBytes = 0) => new() { _rd.BufferGetData(Rid, offsetBytes, sizeBytes) };
    
    }

}