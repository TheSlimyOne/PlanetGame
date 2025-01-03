using System;
using Godot;
using Dispatcher;
using Godot.Collections;
using UniformException;

namespace Uniform
{
    public partial class MultimeshUniform : ComputeShaderUniform
    {
        // public class MultimeshParameters
        // {
        //     public Rid Multimesh { get; protected set; }
        //     public Rid Mesh { get; set; }
        //     public Rid Scenario { get; set; }
        //     public Rid Instance { get; set; }
        //     public float ExtraVisibilityMargin { get; set; }
        //     public int InstanceCount { get; set; }
        //     public int VisibleInstances { get; set; } = -1;
        //     public Transform3D Transform3D { get; set; } = Transform3D.Identity;
        //     public uint LayerMask { get; set; } = 0;

        //     protected internal void ApplyMultiMeshBufferParameters()
        //     {
        //         RenderingServer.MultimeshAllocateData(Multimesh, InstanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, colorFormat: true, customDataFormat: true, useIndirect: true);
        //         RenderingServer.MultimeshSetMesh(Multimesh, Mesh);
        //         RenderingServer.MultimeshSetVisibleInstances(Multimesh, VisibleInstances);

        //         CreateMultimeshInstance()
        //     }

        //     public MultimeshParameters()
        //     {
        //         Multimesh = RenderingServer.MultimeshCreate();
        //     }

        //     public Rid CreateMultimeshInstance()
        //     {
        //         RenderingServer.InstanceSetBase(Instance, Multimesh);
        //         RenderingServer.InstanceSetTransform(Instance, Transform3D);
        //         RenderingServer.InstanceSetScenario(Instance, Scenario);
        //         RenderingServer.InstanceGeometrySetFlag(Instance, RenderingServer.InstanceFlags.UseDynamicGI, true);
        //         RenderingServer.InstanceGeometrySetCastShadowsSetting(Instance, RenderingServer.ShadowCastingSetting.On);
        //         RenderingServer.InstanceSetExtraVisibilityMargin(Instance, ExtraVisibilityMargin);
        //         RenderingServer.InstanceSetLayerMask(Instance, LayerMask);
        //     }
        // }

        // public MultimeshParameters Parameters { get; private set; }

        
        public bool IsCommandBuffer { get; set; }
        public Rid Multimesh { get; private set; }
        private Array<Rid> Instances { get; set; } = new Array<Rid>();
        public MultimeshUniform(IDispatchable owner, int binding, int instanceCount, Rid mesh, int visibleInstances) : base(RenderingServer.GetRenderingDevice(), binding, owner)
        {
            CreateMultimesh(instanceCount, mesh, visibleInstances);
            IsCommandBuffer = false;
            Rid = RenderingServer.MultimeshGetBufferRdRid(Multimesh);

            Uniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = binding
            };
            Uniform.AddId(Rid);

        }

        public MultimeshUniform(IDispatchable owner, int binding, Rid multimesh) : base(RenderingServer.GetRenderingDevice(), binding, owner)
        {
            IsCommandBuffer = true;
            Rid = RenderingServer.MultimeshGetCommandBufferRdRid(multimesh);

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
            Multimesh = multimeshUniform.Multimesh;
            Instances = multimeshUniform.Instances;

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

        public void CreateMultimesh(int instanceCount, Rid mesh, int visibleInstances)
        {
            Multimesh = RenderingServer.MultimeshCreate();
            RenderingServer.MultimeshAllocateData(Multimesh, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, colorFormat: true, customDataFormat: true, useIndirect: true);
            RenderingServer.MultimeshSetMesh(Multimesh, mesh);
            RenderingServer.MultimeshSetVisibleInstances(Multimesh, visibleInstances);
        }

        public override void UpdateUniform(byte[] data)
        {
            _rd.BufferUpdate(Rid, 0, (uint)data.Length, data);
        }

        public override Array<byte[]> GetByteData() => new() { _rd.BufferGetData(Rid) };

        public Rid CreateMultimeshInstance(Transform3D transform, Rid scenario, float extraVisibilityMargin, uint layerMask)
        {
            Rid instance = RenderingServer.InstanceCreate();
            RenderingServer.InstanceSetBase(instance, Multimesh);
            RenderingServer.InstanceSetTransform(instance, transform);
            RenderingServer.InstanceSetScenario(instance, scenario);
            RenderingServer.InstanceGeometrySetFlag(instance, RenderingServer.InstanceFlags.UseDynamicGI, true);
            RenderingServer.InstanceGeometrySetCastShadowsSetting(instance, RenderingServer.ShadowCastingSetting.On);
            RenderingServer.InstanceSetExtraVisibilityMargin(instance, extraVisibilityMargin);
            RenderingServer.InstanceSetLayerMask(instance, layerMask);
            Instances.Add(instance);
            return instance;
        }

        public void RemoveMultimeshInstance(Rid instance)
        {
            RenderingServer.FreeRid(instance);
            Instances.Remove(instance);
        }

        public override void FreeRids()
        {
            if (IsCommandBuffer)
            {
                base.FreeRids();
            }
            else
            {
                RenderingServer.FreeRid(Multimesh);
                foreach (Rid instance in Instances)
                {
                    RemoveMultimeshInstance(instance);
                }
            }
        }
    }
}