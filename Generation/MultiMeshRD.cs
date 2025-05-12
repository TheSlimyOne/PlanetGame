using System.Collections.Generic;
using Godot;

public class MultiMeshRD
{

    public Rid MultiMesh { get; private set; }
    public Rid CommandBuffer => RenderingServer.MultimeshGetCommandBufferRdRid(MultiMesh);
    public Rid Buffer => RenderingServer.MultimeshGetBufferRdRid(MultiMesh);
    
    public readonly List<Rid> Instances = [];

    public MultiMeshRD(int instanceCount, Rid mesh, int visibleInstances)
    {
        MultiMesh = RenderingServer.MultimeshCreate();
        RenderingServer.MultimeshAllocateData(MultiMesh, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, colorFormat: true, customDataFormat: true, useIndirect: true);
        RenderingServer.MultimeshSetMesh(MultiMesh, mesh);
        RenderingServer.MultimeshSetVisibleInstances(MultiMesh, visibleInstances);
    }

    public Rid CreateMultimeshInstance(Transform3D transform, Rid materialOverride, Rid scenario, float extraVisibilityMargin, uint layerMask)
    {
        Rid instance = RenderingServer.InstanceCreate();
        RenderingServer.InstanceSetBase(instance, MultiMesh);
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
        foreach(Rid instance in Instances)
        {
            RenderingServer.FreeRid(instance);
        }
        RenderingServer.FreeRid(CommandBuffer);
        RenderingServer.FreeRid(Buffer);
        RenderingServer.FreeRid(MultiMesh);

        Instances.Clear();
    }

}