using System;
using Godot;

namespace Uniform;

public partial class PlaceholderUniform : ComputeShaderUniform
{
    public PlaceholderUniform() : base(null, -1) {}
    public override void UpdateUniform(byte[] data, uint layer = 0) => throw new NotImplementedException("PlaceholderUniform cannot be updated.");
    public override byte[] GetByteData(uint layer = 0) => throw new NotImplementedException();
    public override StorageBufferUniform RebindUniform(RenderingDevice rd, int binding) => throw new NotImplementedException("PlaceholderUniform cannot be rebounded to.");
}
