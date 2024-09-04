using Godot;
using System;

namespace Uniform;

public abstract partial class ComputeShaderUniform : GodotObject
{
    public Rid Rid { get; set; }
    public int Binding { get; set; }
    
    protected RenderingDevice _rd;
    public RDUniform Uniform { get; set; }

    protected ComputeShaderUniform(RenderingDevice renderingDevice, int binding)
    {
        _rd = renderingDevice;
        Binding = binding;
    }

    public abstract ComputeShaderUniform RebindUniform(int binding);

    public abstract void UpdateUniform(byte[] data);
    
    public void FreeRid()
    {
        if (Rid.Id != 0) _rd.FreeRid(Rid);
    }

    public T[] GetData<T>() where T : unmanaged
    {
        return Utilities.FromBytes<T>(_rd.BufferGetData(Rid)).ToArray();
    }

    public byte[] GetByteData()
    {
        return _rd.BufferGetData(Rid);
    }

    ~ComputeShaderUniform()
    {
        FreeRid();
    }
}