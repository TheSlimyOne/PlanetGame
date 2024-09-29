using Godot;
using System;
using Godot.Collections;

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

    public abstract void UpdateUniform(byte[] data, uint layer = 0);
    
    public abstract byte[] GetByteData(uint layer = 0);
    
    public virtual void FreeRid()
    {
        if (Rid.IsValid) _rd.FreeRid(Rid);
        Uniform.ClearIds();
        _rd = null;
    }
}