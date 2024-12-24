using System;
using Godot;
using Dispatcher;
using Godot.Collections;
using UniformException;

namespace Uniform
{
    public partial class MultimeshUniform : ComputeShaderUniform
    {
        public class MultimeshParameters
        {
           public Rid Multimesh { get; protected set; } = RenderingServer.MultimeshCreate();
           public Rid Mesh { get; set; }
           public Rid Scenario { get; set; }
           public Rid Instance { get; set; }
           public float ExtraVisibilityMargin { get; set; }
           public int InstanceCount { get; set; }
           public int VisibleInstances { get; set; } = -1;
           public Transform3D Transform3D { get; set; } = Transform3D.Identity;

            protected internal void ApplyMultiMeshBufferParameters()
            {
                RenderingServer.MultimeshAllocateData(Multimesh, InstanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, colorFormat: true, customDataFormat: true, useIndirect: true);
                RenderingServer.MultimeshSetMesh(Multimesh, Mesh);
                RenderingServer.MultimeshSetVisibleInstances(Multimesh, VisibleInstances);
                
                RenderingServer.InstanceSetBase(Instance, Multimesh);
                RenderingServer.InstanceSetTransform(Instance, Transform3D);
                RenderingServer.InstanceSetScenario(Instance, Scenario);
                RenderingServer.InstanceGeometrySetFlag(Instance, RenderingServer.InstanceFlags.UseDynamicGI, true);
                RenderingServer.InstanceGeometrySetCastShadowsSetting(Instance, RenderingServer.ShadowCastingSetting.On);
                RenderingServer.InstanceSetExtraVisibilityMargin(Instance, ExtraVisibilityMargin);
            }
        }

        public MultimeshParameters Parameters { get; private set; }
        public bool IsCommandBuffer { get; set; }

        public MultimeshUniform(IDispatchable owner, MultimeshParameters parameters, int binding, bool isCommandBuffer) : base(RenderingServer.GetRenderingDevice(), binding, owner)
        {
            Parameters = parameters;
            if (!IsCommandBuffer)
                ApplyMultiMeshBufferParameters();
            IsCommandBuffer = isCommandBuffer;
            Rid = IsCommandBuffer ? RenderingServer.MultimeshGetCommandBufferRdRid(Parameters.Multimesh) : RenderingServer.MultimeshGetBufferRdRid(Parameters.Multimesh);
            
            Uniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = binding
            };
            Uniform.AddId(Rid);

        }

        private MultimeshUniform(IDispatchable owner, MultimeshUniform multimeshUniform, int binding) : base(multimeshUniform._rd, binding, owner)
        {
            Rid = multimeshUniform.Rid;
            IsCommandBuffer = multimeshUniform.IsCommandBuffer;
            Parameters = multimeshUniform.Parameters;

            Uniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = binding
            };

            foreach (Rid rid in multimeshUniform.Uniform.GetIds())
            {
                Uniform.AddId(rid);
            }
        }

        public override MultimeshUniform RebindUniform(IDispatchable owner, RenderingDevice rd, int binding)
        {
            if (rd == _rd)
                return new MultimeshUniform(Owner, this, binding);
            else
                throw new InvalidRenderingDeviceException();
        }

        public T[] GetData<T>() where T : unmanaged => Utilities.FromBytes<T>(_rd.BufferGetData(Rid)).ToArray();

        public override void UpdateUniform(byte[] data)
        {
            _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);
        }

        public override Array<byte[]> GetByteData() => new() { _rd.BufferGetData(Rid) };

        public override void FreeRids()
        {
            if (IsCommandBuffer)
            {
            }
            else
            {
                RenderingServer.FreeRid(Parameters.Multimesh);
                RenderingServer.FreeRid(Parameters.Instance);

                base.FreeRids();
            }
        }
        
        public void ApplyMultiMeshBufferParameters()
        {
            Parameters.ApplyMultiMeshBufferParameters();
        }

    }
}