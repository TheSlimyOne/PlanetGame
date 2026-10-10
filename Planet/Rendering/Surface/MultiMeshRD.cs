using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Shaders;
using PlanetGame.Util;
using Uniform;

public class MultiMeshRD : IGPUResource
{

    public Rid Rid { get; private set; }
    public Mesh Mesh { get; private set; }
    public StorageBufferUniform CommandBufferUniform { get; private set; }
    public StorageBufferUniform BufferUniform { get; private set; }
    public StorageBufferUniform MeshDataUniform { get; private set; }

    public event Action BuffersChanged;

    public Rid CommandBuffer => RenderingServer.MultimeshGetCommandBufferRdRid(Rid);
    public Rid Buffer => RenderingServer.MultimeshGetBufferRdRid(Rid);

    public readonly List<Rid> Instances = [];

    public MultiMeshRD(int instanceCount, Mesh mesh, int visibleInstances)
    {
        Rid = RenderingServer.MultimeshCreate();

        RenderingDevice renderingDevice = RenderingServer.GetRenderingDevice();
        CommandBufferUniform = new("Multimesh Command Buffer", this, renderingDevice, perserved: true);
        BufferUniform = new("Multimesh Buffer", this, renderingDevice, perserved: true);
        MeshDataUniform = new("Mesh Data", this, renderingDevice, data: [.. Utilities.CollectionToBytes([0, 0, 0, 0])], perserved: true);

        RenderingServer.MultimeshAllocateData(Rid, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, colorFormat: true, customDataFormat: true, useIndirect: true);
        RenderingServer.MultimeshSetVisibleInstances(Rid, visibleInstances);
        SetMesh(mesh);
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

    public void SetExtraVisibilityMargin(float extraVisibilityMargin)
    {
        foreach (Rid instance in Instances)
        {
            RenderingServer.InstanceSetExtraVisibilityMargin(instance, extraVisibilityMargin);
        }
    }

    public void SetMesh(Mesh mesh)
    {
        Mesh = mesh;
        RenderingServer.MultimeshSetMesh(Rid, Mesh.GetRid());
        RenderingDevice renderingDevice = RenderingServer.GetRenderingDevice();

        (Vector3[] vertices, int[] indices, Vector3[] _, Vector2[] __) = GetMeshData(mesh);
        byte[] meshData = [.. Utilities.ToBytes([(uint)vertices.Length, (uint)indices.Length])];

        CommandBufferUniform.SetRid(CommandBuffer);
        BufferUniform.SetRid(Buffer);
        MeshDataUniform.UpdateUniform(meshData);

        BuffersChanged?.Invoke();
    }

    public static (Vector3[] vertices, int[] indices, Vector3[] normals, Vector2[] uvs) GetMeshData(Mesh mesh)
    {
        Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
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

        MeshDataUniform?.FreeRid();

        RenderingServer.FreeRid(Rid);
        Rid = default;
        Mesh = null;
    }
}