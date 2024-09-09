using System;
using Uniform;
using Godot;
namespace Shader;

public partial class ComputeNormals : ComputeShader<ComputeNormals.BufferNames>
{
    public PlanetController PlanetController { get; set; }
    
    public enum BufferNames{
        HEIGHT_MAP_DATA,
        HEIGHT_MAP,
        NORMAL_MAP
    }

    public ComputeNormals(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
    {
        SetupComputeShader();
    }

    public override void CreateUniforms()
    {  
        _computeShaderUniforms = new System.Collections.Generic.Dictionary<BufferNames, ComputeShaderUniform>()
        {
            [BufferNames.HEIGHT_MAP_DATA] = new StorageBufferUniform(_rd, (int)BufferNames.HEIGHT_MAP_DATA, Utilities.ToBytes<float>( new float[] {
                PlanetController.PlanetData.Radius,
                PlanetController.PlanetData.HeightScale
            }).ToArray()),

            [BufferNames.HEIGHT_MAP] = new TextureUniform(_rd, (int)BufferNames.HEIGHT_MAP_DATA, PlanetController.PlanetData.HeightMap, imageFormat: Image.Format.L8),

            [BufferNames.NORMAL_MAP] = new TextureUniform(_rd, (int)BufferNames.NORMAL_MAP,
				new RDTextureFormat()
				{
					Width = (uint)PlanetController.PlanetData.HeightMap.GetWidth(),
					Height = (uint)PlanetController.PlanetData.HeightMap.GetHeight(),
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
        Image heightMap = PlanetController.PlanetData.HeightMap.GetImage();

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