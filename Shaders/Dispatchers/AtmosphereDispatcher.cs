using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Planet.Rendering;
using PlanetGame.Util;
using PlanetGame.Data;
using Uniform;
using PlanetGame.Shaders.Dispatchers;

namespace PlanetGame.Shaders.RenderPasses
{
    public partial class AtmosphereDispatcher : Dispatcher<AtmosphereDispatcher.BufferNames>
    {
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.ATMOSPHERE_COMPUTE };
        private static Data.RenderData RenderData => SaveManager.RenderData;
        private static AtmosphereData AtmosphereData => SaveManager.AtmosphereData;

        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedShaderUniforms;

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
            _shaderUniforms = [];

            _shaderUniforms[BufferNames.COLOR_TEXTURE] = new Texture2DUniform(BufferNames.COLOR_TEXTURE.ToString(), null, RenderingDevice,
                RenderingDevice.UniformType.Image,
                perserved: true
            );

            _shaderUniforms[BufferNames.DEPTH_TEXTURE] = new Texture2DUniform(BufferNames.DEPTH_TEXTURE.ToString(), null, RenderingDevice,
                RenderingDevice.UniformType.SamplerWithTexture,
                perserved: true
            );

            _shaderUniforms[BufferNames.LINEAR_DEPTH_TEXTURE] = _sharedShaderUniforms[PlanetRenderer.BufferNames.LINEAR_DEPTH_TEXTURE];
            
            _shaderUniforms[BufferNames.ATMOSPHERE_DATA] = new StorageBufferUniform(BufferNames.ATMOSPHERE_DATA.ToString(), this, RenderingDevice,
				AtmosphereData.ToBytes()
			);

            _shaderUniforms[BufferNames.WORLD_DATA] = _sharedShaderUniforms[PlanetRenderer.BufferNames.WORLD_DATA];

        }

#nullable enable
        protected override void InvokeInternal(object[]? pushConstants = null)
		{
			if (pushConstants == null)
				throw new("Push constants are required");
            Span<byte> pushConstantBytes = Utilities.CollectionToBytes(pushConstants);

            Rid color = GetUniform<Texture2DUniform>(BufferNames.COLOR_TEXTURE).Rid;
            RDTextureFormat format = RenderingDevice.TextureGetFormat(color);

            uint groupX = (format.Width + 7) / 8;
            uint groupY = (format.Height + 7) / 8;

			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
			RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListSetPushConstant(computeList, pushConstantBytes, (uint)pushConstantBytes.Length);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatch(computeList, groupX, groupY, 1);
			RenderingDevice.ComputeListEnd();
		}
#nullable disable

        public override void UpdateUniforms()
        {
            GetUniform<StorageBufferUniform>(BufferNames.ATMOSPHERE_DATA).UpdateUniform(AtmosphereData.ToBytes());
        }

        public void UpdateUniforms(Rid color, Rid depth)
        {
            Texture2DUniform colorUniform = GetUniform<Texture2DUniform>(BufferNames.COLOR_TEXTURE);
            Texture2DUniform depthUniform = GetUniform<Texture2DUniform>(BufferNames.DEPTH_TEXTURE);            
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

            UpdateUniforms();
            
            if (changed)
                CreateUniformSet();

        }
    }
}