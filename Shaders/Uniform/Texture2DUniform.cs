using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlanetGame.Shaders;
using UniformException;

namespace Uniform
{
	public class Texture2DUniform : ShaderUniform
	{
		public Rid SamplerRid { get; protected set; }
		public RDTextureFormat TextureFormat { get; protected set; }
		public RDSamplerState SamplerState { get; protected set; }

		public void SetRid(Rid rid, RDTextureFormat textureFormat = null, bool perserved = true, bool freePreviousRid = false)
		{
			if (Rid != rid && freePreviousRid && !Perserved && Rid.IsValid && RenderingDevice.TextureIsValid(Rid))
				RenderingDevice.FreeRid(Rid);

			Rid = rid;
			TextureFormat = textureFormat ?? RenderingDevice.TextureGetFormat(Rid);
			Perserved = perserved;
		}

		public Texture2DUniform(string name, IGPUResource owner, RenderingDevice renderingDevice, RenderingDevice.UniformType uniformType, RDSamplerState samplerState = null, bool perserved = false) : base(name, renderingDevice, owner, perserved)
		{
			Rid = new();
			UniformType = uniformType;
			TextureFormat = null;

			if (UniformType == RenderingDevice.UniformType.SamplerWithTexture)
			{
				SamplerState = samplerState ?? new RDSamplerState();
				SamplerRid = RenderingDevice.SamplerCreate(SamplerState);
			}
		}

		public Texture2DUniform(string name, IGPUResource owner, RenderingDevice renderingDevice, RenderingDevice.UniformType uniformType, Rid rid, RDSamplerState samplerState = null, bool perserved = false) : base(name, renderingDevice, owner, perserved)
		{
			Rid = rid;
			UniformType = uniformType;
			TextureFormat = RenderingDevice.TextureGetFormat(Rid);
			
			if (UniformType == RenderingDevice.UniformType.SamplerWithTexture)
			{
				SamplerState = samplerState ?? new RDSamplerState();
				SamplerRid = RenderingDevice.SamplerCreate(SamplerState);
			}
		}

		public void SaveImage(string path, Image.Format format, uint layer = 0)
		{
			Error error = GetImage(format, layer).SavePng(path + ".png");

			if (error != Error.Ok)
			{
				GD.PrintErr($"Failed to save image: {error}");
			}
			else
			{
				GD.Print($"Image saved successfully to {path}.png");
			}
		}

		public Texture2Drd GetTexture2Drd() => new() { TextureRdRid = Rid };
		public Texture2DArrayRD GetTexture2DArrayRD() => new() { TextureRdRid = Rid };

		public Image GetImage(Image.Format format, uint layer = 0) => Image.CreateFromData((int)TextureFormat.Width, (int)TextureFormat.Height, false, format, GetLayerByteData(layer));

		public Image[] GetImageArray(Image.Format format)
		{
			Image[] images = new Image[TextureFormat.ArrayLayers];

			for (uint i = 0; i < images.Length; i++)
				images[i] = Image.CreateFromData((int)TextureFormat.Width, (int)TextureFormat.Height, false, format, GetLayerByteData(i));

			return images;
		}

		public byte[] GetLayerByteData(uint layer) => RenderingDevice.TextureGetData(Rid, layer);

		public Color GetPixel(int x, int y)
		{
			Image.Format imageFormat = FormatConverter.MatchDataFormat(TextureFormat.Format);
			return GetImage(imageFormat).GetPixel(x, y);
		}

		public Color GetPixel(Vector2I at) => GetPixel(at.X, at.Y);

		public Vector2I GetSize() => new((int)TextureFormat.Width, (int)TextureFormat.Height);

		public virtual void ClearTexture(Color color) => RenderingDevice.TextureClear(Rid, color, 0, 1, 0, 1);

		public virtual void ClearTexture(Color color, uint baseMipmap = 0, uint mipmapCount = 1, uint baseLayer = 0, uint layerCount = 1) => RenderingDevice.TextureClear(Rid, color, baseMipmap, mipmapCount, baseLayer, layerCount);

		public override void UpdateUniform(byte[] data) => RenderingDevice.TextureUpdate(Rid, 0, data);

		public void SetImage(Image image) => UpdateUniform(image.GetData());

		public void SetImage(Image[] images)
		{
			for (uint i = 0; i < images.Length; i++)
				RenderingDevice.TextureUpdate(Rid, i, images[i].GetData());
		}

		public override List<byte[]> GetByteData()
		{
			List<byte[]> data = [];

			for (uint i = 0; i < TextureFormat.ArrayLayers; i++)
				data.Add(GetLayerByteData(i));

			return data;
		}
		
		public static byte[] CreateSolidColorImage(int width, int height, Image.Format format, Color color)
		{
			Image image = Image.CreateEmpty(width, height, false, format);
			image.Fill(color);
			return image.GetData();
		}

        public override RDUniform CreateRDUniform(int binding)
        {
            RDUniform uniform = new()
			{
				UniformType = UniformType,
				Binding = binding
			};

			if (UniformType == RenderingDevice.UniformType.SamplerWithTexture)
				uniform.AddId(SamplerRid);

			uniform.AddId(Rid);
			return uniform;
        }

		protected override void FreeRidInternal()
		{
			if (RenderingDevice == null)
				return;
				
			if (SamplerRid.IsValid)
				RenderingDevice.FreeRid(SamplerRid);

			if(Rid.IsValid && RenderingDevice.TextureIsValid(Rid))
				RenderingDevice.FreeRid(Rid);

			Rid = new();
			SamplerRid = new();
			SamplerState = default;
			TextureFormat = default;
		}
	}
}