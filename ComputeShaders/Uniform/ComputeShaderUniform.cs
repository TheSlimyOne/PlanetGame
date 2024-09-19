using Godot;
using System;

namespace Uniform;

public abstract partial class ComputeShaderUniform : GodotObject
{
    public Rid Rid { get; protected set; }
    public int Binding { get; protected set; }
    public RDUniform Uniform { get; protected set; }
    protected RenderingDevice _rd;

    protected ComputeShaderUniform(RenderingDevice renderingDevice, int binding)
    {
        _rd = renderingDevice;
        Binding = binding;
    }

    public abstract ComputeShaderUniform RebindUniform(RenderingDevice rd, int binding);

    public abstract void UpdateUniform(byte[] data);
    
    public virtual void FreeRid()
    {
        if (Rid.IsValid) _rd.FreeRid(Rid);
        Uniform.ClearIds();
        _rd = null;
    }

    public T[] GetData<T>() where T : unmanaged
    {
        return Utilities.FromBytes<T>(_rd.BufferGetData(Rid)).ToArray();
    }

    public virtual byte[] GetByteData()
    {
        return _rd.BufferGetData(Rid);
    }
}