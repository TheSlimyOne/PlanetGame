using Godot;
using System;

namespace ComputeShaderClasses;

public abstract partial class ComputeShaderUniform : GodotObject
{
    public Rid Rid;
    public int Binding;
    
    public RenderingDevice RenderingDevice;
    public RDUniform Uniform;
    private byte[] data;

    public ComputeShaderUniform(RenderingDevice renderingDevice, int binding)
    {
        RenderingDevice = renderingDevice;
        Binding = binding;
    }

    public abstract void UpdateUniform(byte[] data);

    public void FreeRid()
    {
        RenderingDevice.FreeRid(Rid);
    }

    ~ComputeShaderUniform()
    {
        if (Rid.Id != 0) FreeRid();
    }


}