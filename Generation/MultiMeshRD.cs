using System.Collections.Generic;
using Godot;

public class MultiMeshRD
{

    public Rid Rid { get; private set; }
    public Rid CommandBuffer => RenderingServer.MultimeshGetCommandBufferRdRid(Rid);
    public Rid Buffer => RenderingServer.MultimeshGetBufferRdRid(Rid);
    
    public readonly List<Rid> Instances = [];

    public MultiMeshRD(int instanceCount, Rid mesh, int visibleInstances)
    {
        Rid = RenderingServer.MultimeshCreate();
        RenderingServer.MultimeshAllocateData(Rid, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, colorFormat: true, customDataFormat: true, useIndirect: true);
        RenderingServer.MultimeshSetMesh(Rid, mesh);
        RenderingServer.MultimeshSetVisibleInstances(Rid, visibleInstances);
    }

    public Rid CreateMultimeshInstance(Transform3D transform, Rid materialOverride, Rid scenario, float extraVisibilityMargin, uint layerMask)
    {
        Rid instance = RenderingServer.InstanceCreate();
        RenderingServer.InstanceSetBase(instance, Rid);
        RenderingServer.InstanceSetTransform(instance, transform);
        RenderingServer.InstanceSetScenario(instance, scenario);
        RenderingServer.InstanceGeometrySetFlag(instance, RenderingServer.InstanceFlags.UseDynamicGI, true);
        RenderingServer.InstanceGeometrySetCastShadowsSetting(instance, RenderingServer.ShadowCastingSetting.On);
        RenderingServer.InstanceSetExtraVisibilityMargin(instance, extraVisibilityMargin);
        RenderingServer.InstanceSetLayerMask(instance, layerMask);
        RenderingServer.InstanceGeometrySetMaterialOverride(instance, materialOverride);
        
        Instances.Add(instance);
        return instance;
    }

    public void CleanupGPU()
    {
        RenderingServer.FreeRid(Buffer);
        RenderingServer.FreeRid(CommandBuffer);
        RenderingServer.FreeRid(Rid);
        foreach(Rid instance in Instances)
        {
            RenderingServer.FreeRid(instance);
        }

        Instances.Clear();
    }

}