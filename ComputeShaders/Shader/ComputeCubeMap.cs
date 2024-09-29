using System;
using Uniform;
using Godot;
namespace Shader;

public partial class ComputeCubeMap : ComputeShader<ComputeCubeMap.BufferNames>
{ 
    public enum BufferNames{
        IMAGE_TEXTURE,
        CUBE_TEXTURE,
    }

    public Texture2D InputTexture;

	public ComputeCubeMap(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
	{
		SetupComputeShader();
	}

    public override void CreateUniforms()
    {
        // _computeShaderUniforms = new System.Collections.Generic.Dictionary<BufferNames, ComputeShaderUniform>()
        // {
            
        //     [BufferNames.IMAGE_TEXTURE] = new Texture2DUniform(_rd, (int)BufferNames.IMAGE_TEXTURE, PlanetController.PlanetData.HeightMap, imageFormat: Image.Format.L8),
        //     [BufferNames.CUBE_TEXTURE] = new Texture2DUniform(_rd, (int)BufferNames.CUBE_TEXTURE, null, imageFormat: Image.Format.L8),

        // };
        // CreateUniformSet();
    }

    public override void Ready()
    {
        throw new NotImplementedException();
    }

    public override void UpdateUniforms()
    {
        throw new NotImplementedException();
    }

    public void SaveCubeMap()
    {

    }
}