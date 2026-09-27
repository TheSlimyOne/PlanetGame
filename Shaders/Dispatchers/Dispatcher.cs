using System;
using Godot;

namespace PlanetGame.Shaders.Dispatchers
{
    public abstract class Dispatcher<TEnum>(RenderingDevice renderingDevice, ShaderProgramPaths shaderPath) 
        : ShaderPass<TEnum>(renderingDevice, shaderPath) where TEnum : Enum
    {
        protected Dispatcher(ShaderProgramPaths shaderPath) : this(RenderingServer.GetRenderingDevice(), shaderPath) { }
   
        public void SetupShader()
        {
            CreateShader();
            CreatePipeline();
        }

        public override bool IsValid() => RenderingDevice != null &&  RenderingDevice.UniformSetIsValid(_uniformSet) &&  _shader.IsValid && RenderingDevice.ComputePipelineIsValid(_pipeline);
    }
}