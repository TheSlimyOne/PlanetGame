using System;
using Godot;
using Godot.Collections;
using Dispatcher;

namespace Uniform
{
    public partial class PlaceholderUniform : ComputeShaderUniform
    {
        public PlaceholderUniform() : base(null, -1, null) {}
        public override void UpdateUniform(byte[] data) => throw new NotImplementedException("PlaceholderUniforms cannot be updated.");
        public override Array<byte[]> GetByteData() => throw new NotImplementedException("PlaceholderUniforms does not contain data.");
        public override StorageBufferUniform RebindUniform(IDispatchable owner, RenderingDevice rd, int binding) => throw new NotImplementedException("PlaceholderUniforms cannot be rebounded to.");
    }
}
