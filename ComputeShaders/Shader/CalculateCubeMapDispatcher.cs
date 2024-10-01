using System;
using Uniform;
using Godot;
using Godot.Collections;
namespace Shader;

public partial class CalculateCubeMapDispatcher : ComputeShaderDispatcher<CalculateCubeMapDispatcher.BufferNames>
{
    public enum BufferNames
    {
        IMAGE_TEXTURE,
        CUBE_TEXTURE
    }

    public const int INNOVCATIONS = 8;

    public Texture2D InputTexture;

    public CalculateCubeMapDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
    {
        SetupComputeShader();
    }

    public override void CreateUniforms()
    {
        int cubeFaceSize = InputTexture.GetHeight() / 2;

        _computeShaderUniforms = new System.Collections.Generic.Dictionary<BufferNames, ComputeShaderUniform>()
        {

            [BufferNames.IMAGE_TEXTURE] = new Func<Texture2DUniform>(() =>
            {
                Image image = InputTexture.GetImage();
                image.ClearMipmaps();
                image.Convert(Image.Format.Rgbaf);

                return new Texture2DUniform(_rd, (int)BufferNames.IMAGE_TEXTURE,
                    new RDTextureFormat()
                    {
                        Width = (uint)image.GetWidth(),
                        Height = (uint)image.GetHeight(),
                        TextureType = RenderingDevice.TextureType.Type2D,
                        Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                        UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
                    }, RenderingDevice.UniformType.SamplerWithTexture, textureData: new() { image.GetData() });
            }).Invoke(),

            [BufferNames.CUBE_TEXTURE] = new Texture2DUniform(_rd, (int)BufferNames.CUBE_TEXTURE,
                new RDTextureFormat()
                {
                    Width = (uint)cubeFaceSize,
                    Height = (uint)cubeFaceSize,
                    ArrayLayers = 6,
                    TextureType = RenderingDevice.TextureType.Type2DArray,
                    Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                    UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanCopyToBit
                }, RenderingDevice.UniformType.Image,
                textureData: new()
                {
                    Texture2DUniform.CreateSolidColorImage(cubeFaceSize, cubeFaceSize, Image.Format.Rgbaf, VectorUtils.ToColor(VectorUtils.toVector4(Vector3.Up, 1))),
                    Texture2DUniform.CreateSolidColorImage(cubeFaceSize, cubeFaceSize, Image.Format.Rgbaf, VectorUtils.ToColor(VectorUtils.toVector4(Vector3.Down, 1))),
                    Texture2DUniform.CreateSolidColorImage(cubeFaceSize, cubeFaceSize, Image.Format.Rgbaf, VectorUtils.ToColor(VectorUtils.toVector4(Vector3.Left, 1))),
                    Texture2DUniform.CreateSolidColorImage(cubeFaceSize, cubeFaceSize, Image.Format.Rgbaf, VectorUtils.ToColor(VectorUtils.toVector4(Vector3.Right, 1))),
                    Texture2DUniform.CreateSolidColorImage(cubeFaceSize, cubeFaceSize, Image.Format.Rgbaf, VectorUtils.ToColor(VectorUtils.toVector4(Vector3.Forward, 1))),
                    Texture2DUniform.CreateSolidColorImage(cubeFaceSize, cubeFaceSize, Image.Format.Rgbaf, VectorUtils.ToColor(VectorUtils.toVector4(Vector3.Back, 1))),
                }
            ),
        };


        CreateUniformSet();
    }

    public override void Ready()
    {
        int cubeFaceSize = InputTexture.GetHeight() / 2;
        Vector2I numThreads = new Vector2I(cubeFaceSize / INNOVCATIONS, cubeFaceSize / INNOVCATIONS);
        long computeList = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(computeList, _pipeline);
        _rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
        _rd.ComputeListDispatch(computeList, (uint)numThreads.X, (uint)numThreads.Y, 6);
        _rd.ComputeListEnd();
    }

    public override void UpdateUniforms()
    {
        throw new NotImplementedException();
    }

    public void SaveCubeMap(string path)
    {
        GetUniform<Texture2DUniform>(BufferNames.CUBE_TEXTURE).SaveImage(path, Image.Format.Rgbaf);
    }
}