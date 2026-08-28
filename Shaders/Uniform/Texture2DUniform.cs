using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlanetGame.Shaders;
using PlanetGame.Shaders.Dispatchers;
using UniformException;

namespace Uniform
{
	public partial class Texture2DUniform : ShaderUniform
	{
		public RDTextureFormat TextureFormat { get; protected set; }
		public RDSamplerState SamplerState { get; protected set; }

		// TODO prob should implement perserved lol idk how I missed that
		// got it partial done ig
		public Texture2DUniform(IGPUResource owner, RenderingDevice renderingDevice, int binding, RDTextureFormat format, RenderingDevice.UniformType uniformType, List<byte[]> textureData = null, bool perserved = false) : base(renderingDevice, binding, owner, perserved)
		{
			TextureFormat = format;
			Rid = renderingDevice.TextureCreate(TextureFormat, new RDTextureView(), textureData != null ? [.. textureData] : null);
			Uniform = new()
			{
				UniformType = uniformType,
				Binding = binding
			};

			if (uniformType == RenderingDevice.UniformType.Sampler || uniformType == RenderingDevice.UniformType.SamplerWithTexture || uniformType == RenderingDevice.UniformType.SamplerWithTextureBuffer)
			{
				SamplerState = new RDSamplerState()
				{
					// TODO Figure this bs out 
					//MinFilter = RenderingDevice.SamplerFilter.Linear
				};
				Uniform.AddId(RenderingDevice.SamplerCreate(SamplerState));
			}

			Uniform.AddId(Rid);
		}

		public Texture2DUniform(IGPUResource owner, RenderingDevice renderingDevice, int binding, RenderingDevice.UniformType uniformType, Image[] images, bool perserved = false) : base(renderingDevice, binding, owner, perserved)
		{
			RDTextureFormat format;
			Array<byte[]> textureData = [];
			switch (images.Length)
			{
				case 0:
					throw new ArgumentException("Images length is 0.");
				default:
					Image firstImage = images[0];
					format = new()
					{
						Width = (uint)firstImage.GetWidth(),
						Height = (uint)firstImage.GetHeight(),
						ArrayLayers = (uint)images.Length,
						TextureType = RenderingDevice.TextureType.Type2DArray,
						Format = FormatConverter.MatchDataFormat(firstImage.GetFormat()),
						UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
							RenderingDevice.TextureUsageBits.StorageBit |
							RenderingDevice.TextureUsageBits.CanUpdateBit |
							RenderingDevice.TextureUsageBits.CanCopyToBit |
							RenderingDevice.TextureUsageBits.CanCopyFromBit |
							RenderingDevice.TextureUsageBits.ColorAttachmentBit
					};
					foreach (Image image in images)
					{
						if (image.GetSize() != firstImage.GetSize())
							throw new ArgumentException("Images must be the same size.");
						textureData.Add(image.GetData());
					}
					break;
			}

			TextureFormat = format;
			Rid = renderingDevice.TextureCreate(TextureFormat, new RDTextureView(), textureData);
			Uniform = new()
			{
				UniformType = uniformType,
				Binding = binding
			};

			if (uniformType == RenderingDevice.UniformType.Sampler || uniformType == RenderingDevice.UniformType.SamplerWithTexture || uniformType == RenderingDevice.UniformType.SamplerWithTextureBuffer)
			{
				SamplerState = new RDSamplerState()
				{
					// TODO Figure this bs out 
					//MinFilter = RenderingDevice.SamplerFilter.Linear
				};
				Uniform.AddId(RenderingDevice.SamplerCreate(SamplerState));
			}

			Uniform.AddId(Rid);
		}

		private Texture2DUniform(IGPUResource owner, Texture2DUniform textureUniform, int binding) : base(textureUniform.RenderingDevice, binding, owner, textureUniform.Perserved)
		{
			TextureFormat = textureUniform.TextureFormat;
			Rid = textureUniform.Rid;

			Uniform = new()
			{
				UniformType = textureUniform.Uniform.UniformType,
				Binding = binding
			};

			SamplerState = textureUniform.SamplerState;

			foreach (Rid rid in textureUniform.Uniform.GetIds())
			{
				Uniform.AddId(rid);
			}
		}

		public Texture2DUniform(IGPUResource owner, int binding, Rid rid, RenderingDevice.UniformType uniformType, bool perserved = false) : base(binding, owner, perserved)
		{
			Rid = rid;
			TextureFormat = RenderingDevice.TextureGetFormat(Rid);

			Uniform = new()
			{
				UniformType = uniformType,
				Binding = binding
			};

			if (uniformType == RenderingDevice.UniformType.Sampler || uniformType == RenderingDevice.UniformType.SamplerWithTexture || uniformType == RenderingDevice.UniformType.SamplerWithTextureBuffer)
			{
				SamplerState = new RDSamplerState()
				{
					// TODO Figure this bs out 
					//MinFilter = RenderingDevice.SamplerFilter.Linear
					MagFilter = RenderingDevice.SamplerFilter.Linear,
					MinFilter = RenderingDevice.SamplerFilter.Linear,
					RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge,
					RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge,
					RepeatW = RenderingDevice.SamplerRepeatMode.ClampToEdge
				};
				Uniform.AddId(RenderingDevice.SamplerCreate(SamplerState));
			}

			Uniform.AddId(Rid);
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
			{
				images[i] = Image.CreateFromData((int)TextureFormat.Width, (int)TextureFormat.Height, false, format, GetLayerByteData(i));
			}
			return images;
		}
		public byte[] GetLayerByteData(uint layer) => RenderingDevice.TextureGetData(Rid, layer);
		public Color GetPixel(int x, int y)
		{
			Image.Format imageFormat = FormatConverter.MatchDataFormat(TextureFormat.Format);
			return GetImage(imageFormat).GetPixel(x, y);
		}
		public Color GetPixel(Vector2I at)
		{
			Image.Format imageFormat = FormatConverter.MatchDataFormat(TextureFormat.Format);
			return GetImage(imageFormat).GetPixelv(at);
		}
		public Vector2I GetSize() => new((int)TextureFormat.Width, (int)TextureFormat.Height);

		public void ClearTexture(Color color) => RenderingDevice.TextureClear(Rid, color, 0, 1, 0, 1);
		public void ClearTexture(Color color, uint baseMipmap = 0, uint mipmapCount = 1, uint baseLayer = 0, uint layerCount = 1) => RenderingDevice.TextureClear(Rid, color, baseMipmap, mipmapCount, baseLayer, layerCount);

		public override void UpdateUniform(byte[] data) => RenderingDevice.TextureUpdate(Rid, 0, data);
		public void UpdateUniform(uint offset, uint sizeBytes, byte[] data) => RenderingDevice.BufferUpdate(Rid, offset, sizeBytes, data);

		public void SetImage(Image image) => UpdateUniform(image.GetData());
		public void SetImage(Image[] images)
		{
			for (uint i = 0; i < images.Length; i++)
			{
				RenderingDevice.TextureUpdate(Rid, i, images[i].GetData());
			}
		}
		
		public override List<byte[]> GetByteData()
		{
			List<byte[]> data = [];
			for (uint i = 0; i < TextureFormat.ArrayLayers; i++)
			{
				data.Add(GetLayerByteData(i));
			}
			return data;
		}

		public override Texture2DUniform RebindUniform(IGPUResource owner, RenderingDevice rd, int binding)
		{
			if (rd == RenderingDevice)
				return new Texture2DUniform(owner, this, binding);
			else if (!UsingMainRenderingDevice)
				return new Texture2DUniform(owner, rd, binding, TextureFormat, Uniform.UniformType, GetByteData());
			else
				throw new InvalidRenderingDeviceException();
		}

		public static byte[] CreateSolidColorImage(int width, int height, Image.Format format, Color color)
		{
			Image image = Image.CreateEmpty(width, height, false, format);
			image.Fill(color);
			return image.GetData();
		}
	}
}
