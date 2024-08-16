using System;
using Godot;

namespace ComputeShaderClasses;

public partial class StorageBufferUniform : ComputeShaderUniform
{
    public StorageBufferUniform(RenderingDevice renderingDevice, int binding, byte[] data, int indirect = 0) : base(renderingDevice, binding)
    {
        Rid = renderingDevice.StorageBufferCreate((uint)data.Length, data, usage: (RenderingDevice.StorageBufferUsage)indirect);
        Uniform = new()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = binding
        };
        Uniform.AddId(Rid);
    }

    public override void UpdateUniform(byte[] data)
    {
        _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);
    }
}