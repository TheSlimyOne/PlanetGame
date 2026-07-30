using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Shaders;
using PlanetGame.Shaders.Dispatchers;

namespace Uniform
{
	public partial class PlaceholderUniform : ShaderUniform
	{
		public PlaceholderUniform() : base(null, -1, null) {}
		public override void UpdateUniform(byte[] data) => throw new NotImplementedException("PlaceholderUniforms cannot be updated.");
		public override List<byte[]> GetByteData() => throw new NotImplementedException("PlaceholderUniforms does not contain data.");
		public override StorageBufferUniform RebindUniform(IGPUResource owner, RenderingDevice rd, int binding) => throw new NotImplementedException("PlaceholderUniforms cannot be rebounded to.");
	}
}
