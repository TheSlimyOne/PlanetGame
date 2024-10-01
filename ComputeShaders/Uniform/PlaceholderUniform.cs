using System;
using Godot;
using Godot.Collections;

namespace Uniform;

public partial class PlaceholderUniform : ComputeShaderUniform
{
    public PlaceholderUniform() : base(null, -1, false) {}
    public override void UpdateUniform(byte[] data) => throw new NotImplementedException("PlaceholderUniform cannot be updated.");
    public override Array<byte[]> GetByteData() => throw new NotImplementedException();
    public override StorageBufferUniform RebindUniform(RenderingDevice rd, int binding) => throw new NotImplementedException("PlaceholderUniform cannot be rebounded to.");
}
