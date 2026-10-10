using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Planet.Rendering;
using PlanetGame.Util;
using PlanetGame.Data;
using Uniform;
using PlanetGame.Shaders.Dispatchers;
using static PlanetGame.Shaders.RenderPasses.AtmosphereDispatcher.BufferNames;
using static PlanetGame.Shaders.RenderPasses.AtmosphereDispatcher.BufferSets;


namespace PlanetGame.Shaders.RenderPasses
{
    public partial class AtmosphereDispatcher : Dispatcher<AtmosphereDispatcher.BufferNames, AtmosphereDispatcher.BufferSets>
    {
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.ATMOSPHERE_COMPUTE };
        private static Data.RenderData RenderData => SaveManager.RenderData;
        private static AtmosphereData AtmosphereData => SaveManager.AtmosphereData;

        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;

        public enum BufferSets
        {
            DEFAULT,
            FRAMEBUFFER
        }

        public enum BufferNames
        {
            COLOR_TEXTURE,
            DEPTH_TEXTURE,
            LINEAR_DEPTH_TEXTURE,
            ATMOSPHERE_DATA,
            WORLD_DATA,
        }

        public AtmosphereDispatcher(Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedShaderUniforms) : base(_shaderPath)
        {
            _sharedShaderUniforms = sharedShaderUniforms;
            SetupShader();
        }

        public override void CreateUniforms()
        {
            this[0, ATMOSPHERE_DATA, DEFAULT] = new StorageBufferUniform(ATMOSPHERE_DATA.ToString(), this, RenderingDevice,
				AtmosphereData.ToBytes()
			);

            this[1, WORLD_DATA, DEFAULT] = _sharedShaderUniforms[PlanetRenderer.BufferNames.WORLD_DATA];

            this[0, COLOR_TEXTURE, FRAMEBUFFER] = new Texture2DUniform(COLOR_TEXTURE.ToString(), null, RenderingDevice,
                RenderingDevice.UniformType.Image,
                perserved: true
            );

            this[1, DEPTH_TEXTURE, FRAMEBUFFER] = new Texture2DUniform(DEPTH_TEXTURE.ToString(), null, RenderingDevice,
                RenderingDevice.UniformType.SamplerWithTexture,
                perserved: true
            );

            this[2, LINEAR_DEPTH_TEXTURE, FRAMEBUFFER] = _sharedShaderUniforms[PlanetRenderer.BufferNames.LINEAR_DEPTH_TEXTURE];
               
            CreateUniformSet(DEFAULT);
        }

#nullable enable
        protected override void InvokeInternal(object[]? pushConstants = null)
		{
			if (pushConstants == null)
				throw new("Push constants are required");
            Span<byte> pushConstantBytes = Utilities.CollectionToBytes(pushConstants);

            Rid color = GetUniform<Texture2DUniform>(COLOR_TEXTURE).Rid;
            RDTextureFormat format = RenderingDevice.TextureGetFormat(color);

            uint groupX = (format.Width + 7) / 8;
            uint groupY = (format.Height + 7) / 8;

			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            ComputeListBindUniformSets(computeList);
            RenderingDevice.ComputeListSetPushConstant(computeList, pushConstantBytes, (uint)pushConstantBytes.Length);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatch(computeList, groupX, groupY, 1);
			RenderingDevice.ComputeListEnd();
		}
#nullable disable

        public override void UpdateUniforms()
        {
            GetUniform<StorageBufferUniform>(ATMOSPHERE_DATA).UpdateUniform(AtmosphereData.ToBytes());
        }

        public void UpdateFramebufferSet(Rid color, Rid depth)
        {
            Texture2DUniform colorUniform = GetUniform<Texture2DUniform>(COLOR_TEXTURE);
            Texture2DUniform depthUniform = GetUniform<Texture2DUniform>(DEPTH_TEXTURE);            
            bool changed = false;

            if (color != colorUniform.Rid)
            {
                colorUniform.SetRid(color);
                changed = true;
            }

            if (depth != depthUniform.Rid)
            {
                depthUniform.SetRid(depth);
                changed = true;
            }

            
            if (changed)
                CreateUniformSet(FRAMEBUFFER);

        }
    }
}