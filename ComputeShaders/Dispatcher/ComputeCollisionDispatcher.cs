using System;
using Godot;

namespace PlanetGame.ComputeShaders.Dispatcher
{
    public partial class ComputeCollisionDispatcher : ComputeShaderDispatcher<ComputeCollisionDispatcher.BufferNames>
    {
        public enum BufferNames {

        }

        public PlanetController PlanetController { get; set; }

        public ComputeCollisionDispatcher(string shaderFilePath, RenderingDevice rd) : base(shaderFilePath, rd)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            throw new NotImplementedException();
        }

        public override void Invoke()
        {
            throw new NotImplementedException();
        }

        public override void UpdateUniforms()
        {
            throw new NotImplementedException();
        }
    }
}