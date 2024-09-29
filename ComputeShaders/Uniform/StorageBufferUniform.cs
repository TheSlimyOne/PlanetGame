using System;
using Godot;
using Godot.Collections;

namespace Uniform;

public partial class StorageBufferUniform : ComputeShaderUniform
{
    public int Indirect { get; private set; }

    public StorageBufferUniform(RenderingDevice renderingDevice, int binding, byte[] data, int indirect = 0) : base(renderingDevice, binding)
    {
        Indirect = indirect;
        Rid = renderingDevice.StorageBufferCreate((uint)data.Length, data, usage: (RenderingDevice.StorageBufferUsage)Indirect);
        Uniform = new()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = binding
        };
        Uniform.AddId(Rid);
    }

    public StorageBufferUniform(StorageBufferUniform storageBufferUniform, int binding) : base(storageBufferUniform._rd, binding)
    {
        Rid = storageBufferUniform.Rid;
        Indirect = storageBufferUniform.Indirect;
        Uniform = new()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = binding
        };
        Uniform.AddId(Rid);
    }

    public override StorageBufferUniform RebindUniform(RenderingDevice rd, int binding)
    {
        if (rd == _rd)
            return new StorageBufferUniform(this, binding);
        else
            return new StorageBufferUniform(rd, binding, GetByteData(), Indirect);
    }

    public T[] GetData<T>() where T : unmanaged => Utilities.FromBytes<T>(_rd.BufferGetData(Rid)).ToArray();


    public override void UpdateUniform(byte[] data, uint layer = 0)
    {
        _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);
    }

    public override byte[] GetByteData(uint layer = 0) => _rd.BufferGetData(Rid);

}