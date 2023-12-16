using Godot;
using System;
using System.Collections;
[Tool]
public partial class DebugManager : Node3D
{
    public static DebugManager debugManager {get; private set;}
    StandardMaterial3D material = new StandardMaterial3D();
    ArrayList containers = new();

    public void Clear(int containerIndex)
    {
        MeshInstance3D container = (MeshInstance3D)containers[containerIndex];
        container.Mesh = new ImmediateMesh();

        ((ImmediateMesh)container.Mesh).ClearSurfaces();
    }

    public void DrawLine(int containerIndex, Vector3 start, Vector3 end, Color color)
    {
        MeshInstance3D container = (MeshInstance3D)containers[containerIndex];

        ((ImmediateMesh)container.Mesh).SurfaceBegin(Mesh.PrimitiveType.Lines);
        ((ImmediateMesh)container.Mesh).SurfaceSetColor(color);
        ((ImmediateMesh)container.Mesh).SurfaceAddVertex(start);
        ((ImmediateMesh)container.Mesh).SurfaceAddVertex(end);
        ((ImmediateMesh)container.Mesh).SurfaceEnd();
        container.MaterialOverride = material;
    }

    public int GenerateNewContainer()
    {
        MeshInstance3D container = new MeshInstance3D();
        container.Mesh = new ImmediateMesh();
        containers.Add(container);
        AddChild(container);

        return containers.Count - 1;
    }

    public void DrawCube(int containerIndex, Vector3 lowerBounds, Vector3 upperBounds, Color color)
    {
        Vector3[] points = new Vector3[]{
            lowerBounds,
            new Vector3(lowerBounds.X, lowerBounds.Y, upperBounds.Z),
            new Vector3(upperBounds.X, lowerBounds.Y, upperBounds.Z),
            new Vector3(upperBounds.X, lowerBounds.Y, lowerBounds.Z),
            upperBounds,
            new Vector3(upperBounds.X, upperBounds.Y, lowerBounds.Z),
            new Vector3(lowerBounds.X, upperBounds.Y, lowerBounds.Z),
            new Vector3(lowerBounds.X, upperBounds.Y, upperBounds.Z)

        };

        DrawLine(containerIndex, points[0], points[1], color);
        DrawLine(containerIndex, points[1], points[2], color);
        DrawLine(containerIndex, points[2], points[3], color);
        DrawLine(containerIndex, points[3], points[0], color);

        DrawLine(containerIndex, points[0], points[6], color);
        DrawLine(containerIndex, points[1], points[7], color);
        DrawLine(containerIndex, points[2], points[4], color);
        DrawLine(containerIndex, points[3], points[5], color);

        DrawLine(containerIndex, points[4], points[5], color);
        DrawLine(containerIndex, points[5], points[6], color);
        DrawLine(containerIndex, points[6], points[7], color);
        DrawLine(containerIndex, points[7], points[4], color);
    }

    public void DrawSphere(int containerIndex, Vector3 at, float radius, Color color)
    {
        MeshInstance3D container = (MeshInstance3D)containers[containerIndex];

        int step = 15;
        float sppi = 2 * Mathf.Pi / step;
        Vector3[][] axes = new Vector3[][] {
            new Vector3[] { Vector3.Up, Vector3.Right },
            new Vector3[] { Vector3.Right, Vector3.Forward },
            new Vector3[] { Vector3.Forward, Vector3.Up }
        };

        ((ImmediateMesh)container.Mesh).SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        ((ImmediateMesh)container.Mesh).SurfaceSetColor(color);

        foreach (Vector3[] axis in axes)
            for (int i = 0; i <= step; i++)
                ((ImmediateMesh)container.Mesh).SurfaceAddVertex(at + (axis[0] * radius).Rotated(axis[1], sppi * (i % step)));
        ((ImmediateMesh)container.Mesh).SurfaceEnd();
        container.MaterialOverride = material;
    }
    public override void _Ready()
    {
        // material.NoDepthTest = true;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.VertexColorUseAsAlbedo = true;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
    }

    public override void _EnterTree()
    {
        debugManager = this;
        base._EnterTree();
    }

    public override void _ExitTree()
    {
        debugManager = null;
        base._ExitTree();
    }
}
