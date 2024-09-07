using System;
using System.Linq;
using Uniform;
using Godot;
using Godot.Collections;
using Planet;
namespace Shader;

public partial class ComputeGenerateNormals : ComputeShader<ComputeGenerateNormals.BufferNames>
{
    public PlanetController PlanetController { get; set; }
    public ComputeCull ComputeCullShader { get; set; }
    
    public enum BufferNames{
        HEIGHT_MAP_DATA,
        HEIGHT_MAP,
        HEIGHT_GRADIENT,
        NORMAL_MAP
    }

    public ComputeGenerateNormals(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
    {
        SetupComputeShader();
    }

    public override void CreateUniforms()
    {
        Image heightMap = RenderingServer.Texture2DGet(PlanetController.PlanetData.HeightMap.GetRid());
        _computeShaderUniforms = new System.Collections.Generic.Dictionary<BufferNames, ComputeShaderUniform>()
        {
            [BufferNames.HEIGHT_MAP_DATA] = new StorageBufferUniform(_rd, (int)BufferNames.HEIGHT_MAP_DATA, Utilities.ToBytes<float>( new float[] {
                PlanetController.PlanetData.Radius,
                PlanetController.PlanetData.HeightScale
            }).ToArray()),
            [BufferNames.HEIGHT_MAP] =  ComputeCullShader.GetUniform(ComputeCull.BufferNames.HEIGHT_MAP),
            [BufferNames.HEIGHT_GRADIENT] = ComputeCullShader.GetUniform(ComputeCull.BufferNames.HEIGHT_GRADIENT),
            [BufferNames.NORMAL_MAP] = new TextureUniform(_rd, (int)BufferNames.NORMAL_MAP,
				new RDTextureFormat()
				{
					Width = (uint)heightMap.GetWidth(),
					Height = (uint)heightMap.GetHeight(),
					TextureType = RenderingDevice.TextureType.Type2D,
					Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
					UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
								RenderingDevice.TextureUsageBits.StorageBit |
								RenderingDevice.TextureUsageBits.CanUpdateBit |
								RenderingDevice.TextureUsageBits.CanCopyToBit |
								RenderingDevice.TextureUsageBits.CanCopyFromBit |
								RenderingDevice.TextureUsageBits.ColorAttachmentBit
				}
			),
        };
        CreateUniformSet();
    }

    public void SaveNormalMap(string path)
    {
        GetUniform<TextureUniform>(BufferNames.NORMAL_MAP).SaveImage(path, Image.Format.Rgbaf);
    }

    public override void Ready()
    {
        Image heightMap = RenderingServer.Texture2DGet(PlanetController.PlanetData.HeightMap.GetRid());

        Vector2I numThreads = new Vector2I(heightMap.GetWidth()/8, heightMap.GetHeight()/8);
        long computeList = _rd.ComputeListBegin();
		_rd.ComputeListBindComputePipeline(computeList, _pipeline);
		_rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
		_rd.ComputeListDispatch(computeList, (uint)numThreads.X, (uint)numThreads.Y, 1);
		_rd.ComputeListEnd();
    }

    public override void UpdateUniforms()
    {
        throw new NotImplementedException();
    }    
}