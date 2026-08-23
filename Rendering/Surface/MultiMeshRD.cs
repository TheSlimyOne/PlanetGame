using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PlanetGame.Util;

public class MultiMeshRD
{

    public Rid Rid { get; private set; }
    public Mesh Mesh { get; private set; }
    public Rid CommandBuffer => RenderingServer.MultimeshGetCommandBufferRdRid(Rid);
    public Rid Buffer => RenderingServer.MultimeshGetBufferRdRid(Rid);

    // public event Action MeshChanged;

    public readonly List<Rid> Instances = [];

    public MultiMeshRD(int instanceCount, Mesh mesh, int visibleInstances)
    {
        Rid = RenderingServer.MultimeshCreate();
        Mesh = mesh;
        RenderingServer.MultimeshAllocateData(Rid, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, colorFormat: true, customDataFormat: true, useIndirect: true);
        RenderingServer.MultimeshSetMesh(Rid, Mesh.GetRid());
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

    public void SetExtraVisibilityMargin(float extraVisibilityMargin) {
        foreach(Rid instance in Instances)
        {
            RenderingServer.InstanceSetExtraVisibilityMargin(instance, extraVisibilityMargin);
        }
    }

    public void SetMesh(Mesh mesh)
    {
        // ArgumentNullException.ThrowIfNull(mesh);

        Mesh = mesh;
        RenderingServer.MultimeshSetMesh(Rid, mesh.GetRid());

        // MeshChanged.Invoke();
    }

    public (Vector3[] vertices, int[] indices, Vector3[] normals, Vector2[] uvs) GetMeshData()
    {
        Godot.Collections.Array arrays = Mesh.SurfaceGetArrays(0);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        int[] indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        Vector3[] normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        Vector2[] uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        
        return (vertices, indices, normals, uvs);
    }

    public void CleanupGPU()
    {
        foreach (Rid instance in Instances)
            RenderingServer.FreeRid(instance);

        Instances.Clear();

        RenderingServer.FreeRid(Rid);
        Rid = default;
        Mesh = null;
    }
}