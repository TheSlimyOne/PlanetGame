using System;
using System.Linq;
using Godot;
using Godot.Collections;
using Planet;
using Uniform;

namespace Dispatcher
{
    public partial class ReadFramebufferDispatcher : ComputeShaderDispatcher<ReadFramebufferDispatcher.BufferNames>
    {
        public Viewport Viewport { get; set; }
        public enum BufferNames
        {
            FRAMEBUFFER,
            ATOMIC_COUNTER,
            TEXTURE_IDS
        }

        public ReadFramebufferDispatcher(string shaderFilePath, ref RenderingDevice rd) : base(shaderFilePath, ref rd)
        {
            SetupComputeShader();
        }

        public override void CreateUniforms()
        {
            Vector2I size = ((SubViewport)Viewport).Size;
            

            _computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
            {
                [BufferNames.FRAMEBUFFER] = new Texture2DUniform(this, (int)BufferNames.FRAMEBUFFER, Viewport.GetViewportRid()),

                [BufferNames.ATOMIC_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.ATOMIC_COUNTER,
                    new byte[Utilities.SizeOf<int>()]
                ),

                [BufferNames.TEXTURE_IDS] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.TEXTURE_IDS,
                    new byte[Utilities.SizeOf<Vector4>() * size.X * size.Y]
                )
            };

            CreateUniformSet();
        }  

        public Vector4[] GetData()
        {
            return GetUniformData<Vector4>(BufferNames.TEXTURE_IDS);
        }

        public override void Ready()
        {
            Vector2I size = ((SubViewport)Viewport).Size;
            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, (uint)(size.X/8), (uint)(size.Y/8), 1);
            RenderingDevice.ComputeListEnd();
        }

        public override void UpdateUniforms()
        {
            Vector2I size = ((SubViewport)Viewport).Size;
            byte[] data = new byte[Utilities.SizeOf<Vector4>() * size.X * size.Y];
            GetUniform<StorageBufferUniform>(BufferNames.TEXTURE_IDS).UpdateUniform(data);   
            GetUniform<StorageBufferUniform>(BufferNames.ATOMIC_COUNTER).UpdateUniform(new byte[Utilities.SizeOf<int>()]);   
        }

        public Vector4[] GetTextureIds()
        {
            return GetUniformData<Vector4>(BufferNames.TEXTURE_IDS);
        }
    }
}
