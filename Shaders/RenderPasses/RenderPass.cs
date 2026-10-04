using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlanetGame.Util;
using Uniform;

namespace PlanetGame.Shaders.RenderPasses
{
    public abstract class RenderPass<TEnum>(RenderingDevice renderingDevice, ShaderProgramPaths shaderPath) 
        : ShaderPass<TEnum>(renderingDevice, shaderPath) where TEnum : Enum
    {
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

            public override readonly string ToString() =>
                $"{nameof(RenderGeometry)} {{ VertexArray = {VertexArray}, VertexBuffer = {VertexBuffer}, NormalBuffer = {NormalBuffer}, IndexArray = {IndexArray}, IndexBuffer = {IndexBuffer}, VertexFormat = {VertexFormat} }}";
        }
        
        public void SetupShader(Mesh mesh)
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

        public override void CleanupGPU()
        {
            if (RenderingDevice == null)
                return;

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

            _framebuffer = default;
            _framebufferFormat = 0;

            base.CleanupGPU();
        }

        public override bool IsValid() => RenderingDevice != null &&  RenderingDevice.UniformSetIsValid(_uniformSet) &&  _shader.IsValid && RenderingDevice.RenderPipelineIsValid(_pipeline);
    }
}

