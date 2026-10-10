using System;
using Godot;

namespace PlanetGame.Shaders.Dispatchers
{
    public abstract class Dispatcher<BufferId, SetId>(RenderingDevice renderingDevice, ShaderProgramPaths shaderPath)
        : ShaderPass<BufferId, SetId>(renderingDevice, shaderPath)
        where BufferId : struct, Enum
        where SetId : struct, Enum
    {
        protected Dispatcher(ShaderProgramPaths shaderPath) : this(RenderingServer.GetRenderingDevice(), shaderPath) { }

        public void SetupShader()
        {
            CreateShader();
            CreatePipeline();
        }

        public void ComputeListBindUniformSets(long computeList)
        {
            foreach (SetId setId in Enum.GetValues<SetId>())
            {
                uint set = Convert.ToUInt32(setId);
                RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSets[set], set);
            }
        }

        public override bool IsValid() => RenderingDevice != null && UniformSetsIsValid() && _shader.IsValid && RenderingDevice.ComputePipelineIsValid(_pipeline);
    }
}