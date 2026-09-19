using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlanetGame.Util;
using Uniform;

namespace PlanetGame.Shaders.RenderPasses
{
    public interface IRenderable : IGPUResource { }

    public abstract class RenderPass<TEnum>(RenderingDevice renderingDevice, ShaderProgramPaths shaderPath) : IRenderable where TEnum : Enum
    {
        public RenderingDevice RenderingDevice { get; private set; } = renderingDevice;
        // public Vector2I ViewSize { get; private set; } = viewSize;

        protected ShaderProgramPaths _shaderProgramPaths = shaderPath;
        protected Rid _uniformSet;
        protected Rid _shader;
        protected Rid _pipeline;
        protected Rid _framebuffer;
        protected readonly System.Collections.Generic.Dictionary<string, Rid> _framebufferAttachments = [];

        protected long _framebufferFormat;
        protected RenderGeometry _geometry;

        protected struct RenderGeometry
        {
            public Rid VertexArray;
            public Rid VertexBuffer;
            public Rid NormalBuffer;
            public Rid IndexArray;
            public Rid IndexBuffer;
            public long VertexFormat;
        }

        public string Error { get; private set; } = "";

        protected System.Collections.Generic.Dictionary<Enum, ShaderUniform> _shaderUniforms;

        protected RenderPass(ShaderProgramPaths shaderPath) : this(RenderingServer.GetRenderingDevice(), shaderPath) { }

        public ShaderUniform this[Enum @enum]
        {
            get => GetUniform(@enum);
        }

        public ShaderUniform GetUniform(Enum @enum) => _shaderUniforms[@enum];
        public T GetUniform<T>(Enum @enum) where T : ShaderUniform => (T)_shaderUniforms[@enum];

        // TODO maybe rename this to resetUniforms
        public abstract void UpdateUniforms();

#nullable enable
        public abstract void Invoke(byte[]? pushConstants = null);
        public abstract void CreateUniforms();

        public virtual void SetupShader(Mesh mesh)
        {
            SetFramebufferProperties();
            CreateShader();
            CreateGeometry(mesh);
            CreatePipeline();
        }

        protected void CreateFramebufferFormat(Array<RDAttachmentFormat> attachmentFormats)
        {
            _framebufferFormat = RenderingDevice.FramebufferFormatCreate(attachmentFormats);
        }

        protected void CreateFramebuffer(List<(Rid Texture, string Name)> attachmentData)
        {
            if (RenderingDevice.FramebufferIsValid(_framebuffer))
                RenderingDevice.FreeRid(_framebuffer);

            _framebufferAttachments.Clear();

            Array<Rid> attachments = [];

            foreach((Rid texture, string name) in attachmentData)
            {
                attachments.Add(texture);
                _framebufferAttachments[name] = texture;
            }

            _framebuffer = RenderingDevice.FramebufferCreate(attachments);
        }

        protected void CreateShader()
        {
            if (_shaderProgramPaths.Vertex != "" && _shaderProgramPaths.Fragment != "")
            {
                RDShaderSource shaderSource = SaveManager.LoadGraphicsShaderWithIncludes(_shaderProgramPaths.Vertex, _shaderProgramPaths.Fragment);
                RDShaderSpirV shaderSpirV = RenderingDevice.ShaderCompileSpirVFromSource(shaderSource);
                Rid shader = RenderingDevice.ShaderCreateFromSpirV(shaderSpirV);

                if (!shader.IsValid)
                {
                    GD.PrintErr($"For shader: {_shaderProgramPaths.Vertex}, {_shaderProgramPaths.Fragment}");

                    if (shaderSpirV.CompileErrorVertex.Length > 0)
                    {
                        string vertexError = ShaderError.FormatError(shaderSource.SourceVertex, shaderSpirV.CompileErrorVertex);
                        GD.PrintRich(vertexError);
                        GD.PrintErr(shaderSpirV.CompileErrorVertex.StripEdges().Replace("ERROR: ", ""));
                    }

                    if (shaderSpirV.CompileErrorFragment.Length > 0)
                    {
                        string fragmentError = ShaderError.FormatError(shaderSource.SourceFragment, shaderSpirV.CompileErrorFragment);
                        GD.PrintRich(fragmentError);
                        GD.PrintErr(shaderSpirV.CompileErrorFragment.StripEdges().Replace("ERROR: ", ""));
                    }
                }
                _shader = shader;
            }
            else
            {
                GD.PrintErr($"Invalid shader for a render pass:\n\tVertex: {_shaderProgramPaths.Vertex}\n\tFragment: {_shaderProgramPaths.Fragment}\n\tCompute {_shaderProgramPaths.Compute}");
                _shader = new();
            }
        }

        protected virtual void CreateGeometry(Mesh mesh)
        {
            RenderGeometry geometry = new();
            Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector3[] normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            int[] indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();

            byte[] vertexData = [.. Utilities.ToBytes<Vector3>(vertices)];
            byte[] normalData = [.. Utilities.ToBytes<Vector3>(normals)];

            geometry.VertexFormat = CreateVertexFormat();

            geometry.VertexBuffer = RenderingDevice.VertexBufferCreate(
                (uint)vertexData.Length,
                vertexData
            );

            geometry.NormalBuffer = RenderingDevice.VertexBufferCreate(
                (uint)normalData.Length,
                normalData
            );

            geometry.VertexArray = RenderingDevice.VertexArrayCreate(
                (uint)vertices.Length,
                geometry.VertexFormat,
                [geometry.VertexBuffer, geometry.NormalBuffer]
            );

            byte[] indexData = [.. Utilities.ToBytes<int>(indices)];
            geometry.IndexBuffer = RenderingDevice.IndexBufferCreate((uint)indices.Length, RenderingDevice.IndexBufferFormat.Uint32, indexData);
            geometry.IndexArray = RenderingDevice.IndexArrayCreate(geometry.IndexBuffer, 0, (uint)indices.Length);
            _geometry = geometry;
        }

        protected abstract void SetFramebufferProperties();
        protected abstract void CreatePipeline();

        protected void CreateUniformSet()
        {
            Array<RDUniform> bindings = [];
            for (int i = 0; i < _shaderUniforms.Count; i++)
            {
                TEnum @enum = (TEnum)Enum.ToObject(typeof(TEnum), i);
                ShaderUniform shaderUniform = _shaderUniforms[@enum];

                if (shaderUniform.Owner != this)
                    _shaderUniforms[@enum] = shaderUniform.RebindUniform(this, RenderingDevice, i);

                bindings.Add(_shaderUniforms[@enum].Uniform);
            }

            _uniformSet = RenderingDevice.UniformSetCreate(bindings, _shader, 0);
        }

        public static RDVertexAttribute CreateDefaultVertexAttribute() => new()
        {
            Location = 0,
            Format = RenderingDevice.DataFormat.R32G32B32Sfloat,
            Offset = 0,
            Stride = sizeof(float) * 3,
            Frequency = RenderingDevice.VertexFrequency.Vertex
        };

        public static RDVertexAttribute CreateDefaultNormalAttribute()
        {
            RDVertexAttribute normalAttribute = new()
            {
                Location = 1,
                Format = RenderingDevice.DataFormat.R32G32B32Sfloat,
                Offset = 0,
                Stride = sizeof(float) * 3,
                Frequency = RenderingDevice.VertexFrequency.Vertex
            };
            return normalAttribute;
        }

        public virtual long CreateVertexFormat()
        {
            RDVertexAttribute vertexAttribute = CreateDefaultVertexAttribute();
            RDVertexAttribute normalAttribute = CreateDefaultNormalAttribute();

            return RenderingDevice.VertexFormatCreate([
                vertexAttribute,
                normalAttribute
            ]);
        }

        public bool IsValid()
        {
            return Error == "";
        }

        public virtual void CleanupGPU()
        {
            if (RenderingDevice == null)
                return;

            if (RenderingDevice.UniformSetIsValid(_uniformSet))
                RenderingDevice.FreeRid(_uniformSet);

            if (RenderingDevice.RenderPipelineIsValid(_pipeline))
                RenderingDevice.FreeRid(_pipeline);

            if (_shader.IsValid)
                RenderingDevice.FreeRid(_shader);

            if (RenderingDevice.FramebufferIsValid(_framebuffer))
                RenderingDevice.FreeRid(_framebuffer);

            _framebufferAttachments.Clear();

            if (_geometry.VertexArray.IsValid)
                RenderingDevice.FreeRid(_geometry.VertexArray);

            if (_geometry.IndexArray.IsValid)
                RenderingDevice.FreeRid(_geometry.IndexArray);

            if (_geometry.VertexBuffer.IsValid)
                RenderingDevice.FreeRid(_geometry.VertexBuffer);

            if (_geometry.NormalBuffer.IsValid)
                RenderingDevice.FreeRid(_geometry.NormalBuffer);

            if (_geometry.IndexBuffer.IsValid)
                RenderingDevice.FreeRid(_geometry.IndexBuffer);

            _geometry = default;

            if (_shaderUniforms != null)
            {
                foreach (KeyValuePair<Enum, ShaderUniform> kvp in _shaderUniforms)
                {
                    Enum uniformName = kvp.Key;
                    ShaderUniform shaderUniform = kvp.Value;

                    if (IGPUResource.Verbose)
                        GD.Print("========================");

                    if (IGPUResource.Verbose)
                        GD.Print($"Clearing {uniformName} in {GetType().Name} ID: {GetID()} Owner: {shaderUniform.Owner}");

                    if (shaderUniform.Owner == this)
                    {
                        if (IGPUResource.Verbose)
                            GD.Print(shaderUniform.Rid);

                        shaderUniform.FreeRids();
                    }
                    else if (IGPUResource.Verbose)
                    {
                        GD.Print($"{GetType().Name} does not own this uniform. Not free rid");
                    }

                    if (IGPUResource.Verbose)
                        GD.Print("========================");
                }
            }

            _shaderUniforms = null;

            _uniformSet = default;
            _pipeline = default;
            _shader = default;
            _framebuffer = default;
            _framebufferFormat = 0;

            RenderingDevice = null;
        }

        public int GetID() => GetHashCode();

        public override int GetHashCode()
        {
            HashCode hash = new();

            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                // Combine the enum value and ordinal position into the hash
                hash.Add(value.GetHashCode());
                hash.Add(Enum.GetNames(typeof(TEnum))[value.GetHashCode()]);
            }

            return hash.ToHashCode();
        }
    }
}

