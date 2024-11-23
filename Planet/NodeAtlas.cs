using System;
using Godot;
using Planet;
using Uniform;

public class NodeAtlas
{
    public Texture2Drd NodeAtlasImage { get; private set; }

    public NodeAtlas(RenderingDevice rd, int gridSize)
    {
        Image atlasImage = Image.CreateEmpty(gridSize * 6, gridSize, false, Image.Format.Rgbaf);
        Random random = new Random(12);
        for (int i = 0; i < gridSize * 6; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                atlasImage.SetPixel(i, j, new Color(random.NextSingle(), random.NextSingle(), random.NextSingle(), 1));
            }
        }
        atlasImage.ClearMipmaps();

        Texture2DUniform atlasUniform = new(null, rd, 0, new RDTextureFormat()
        {
            Width = (uint)atlasImage.GetWidth(),
            Height = (uint)atlasImage.GetHeight(),
            TextureType = RenderingDevice.TextureType.Type2D,
            Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
            UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
        }, RenderingDevice.UniformType.Sampler, textureData: new() { atlasImage.GetData() });

        NodeAtlasImage = new() { TextureRdRid = atlasUniform.Rid };
    }

}