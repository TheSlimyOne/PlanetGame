using System;
using Godot;
using Godot.Collections;
using Uniform;

namespace Dispatcher
{
    public partial class ComputeCollisionDispatcher : ComputeShaderDispatcher<ComputeCollisionDispatcher.BufferNames>
    {
        public enum BufferNames {

        }

        public PlanetController PlanetController { get; set; }

        public ComputeCollisionDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            throw new NotImplementedException();
        }

        public override void Ready()
        {
            throw new NotImplementedException();
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }
}