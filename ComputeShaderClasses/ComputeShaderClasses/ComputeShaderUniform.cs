using Godot;
using System;

namespace ComputeShaderClasses;

public abstract partial class ComputeShaderUniform : GodotObject
{
    public Rid Rid;
    public int Binding;
    
    public RenderingDevice _rd;
    public RDUniform Uniform;

    public ComputeShaderUniform(RenderingDevice renderingDevice, int binding)
    {
        _rd = renderingDevice;
        Binding = binding;
    }

    public abstract void UpdateUniform(byte[] data);
    
    public void FreeRid()
    {
        _rd.FreeRid(Rid);
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
        if (Rid.Id != 0) FreeRid();
    }


}