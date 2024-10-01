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
    public bool Perserved;

    protected ComputeShaderUniform(RenderingDevice renderingDevice, int binding, bool perserved)
    {
        _rd = renderingDevice;
        Binding = binding;
        Perserved = perserved;
    }

    // This is supposed to simplify the process of sharing buffers between 2 or more compute shaders
    // It will either share the data if the rd is the same or clone the buffer to another rd if the rds are different
    public abstract ComputeShaderUniform RebindUniform(RenderingDevice rd, int binding);

    public abstract void UpdateUniform(byte[] data);

    public abstract Array<byte[]> GetByteData();

    public void FreeRid()
    {
        foreach (Rid rid in Uniform.GetIds())
        {
            _rd.FreeRid(rid);
        }
        Uniform.ClearIds();
        _rd = null;
    }
}