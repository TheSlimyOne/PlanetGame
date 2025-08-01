using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
[Tool]
public partial class test5 : MultiMeshInstance3D
{
    [ExportToolButton("Generate")]
    public Callable Execute => Callable.From(Run);

    [Export] int Amount;
    [Export] Vector2I Size;
    [Export] Vector2 ViewSize;
    [Export] float PointRadius;
    [Export] float HeightScale;

    [Export] Curve ElevationCurve;
    [Export] Curve IsolationDistanceCurve;
    [Export] Curve IsolationDirectionCurve;
    [Export] Curve ProminenceCurve;
    [Export] Curve DominanceGroupsCurve;

    [Export] Texture2D CoarseElevationMap;
    [Export] Texture2D PeakDensityMap;

    [Export] Material LineMaterial;
    [Export] Material DisplayMaterial;
    [Export] MultiMeshInstance3D Lines;
    [Export] MeshInstance3D Display;
    [Export] float num;

    public void Run1()
    {
        Image coarseElevationMap = CoarseElevationMap.GetImage();
        Image peakDensityMap = PeakDensityMap.GetImage();

        if (coarseElevationMap.IsCompressed())
            coarseElevationMap.Decompress();
        if (peakDensityMap.IsCompressed())
            peakDensityMap.Decompress();
        MountainData mountainData = new(new Dictionary<MountainData.MountainStat, (float[] samples, int binCount)>
        {
            {MountainData.MountainStat.ELEVATION, (CurveToArray(ElevationCurve, 1024), 20)},
            {MountainData.MountainStat.ISOLATION_DISTANCE, (CurveToArray(IsolationDistanceCurve, 1024), 20)},
            {MountainData.MountainStat.ISOLATION_DIRECTION, (CurveToArray(IsolationDirectionCurve, 1024), 12)},
            {MountainData.MountainStat.PROMINENCE, (CurveToArray(ProminenceCurve, 1024), 20)},
            {MountainData.MountainStat.DOMINANCE_GROUP, (CurveToArray(DominanceGroupsCurve, 1024), 12)}
        },
            coarseElevationMap, peakDensityMap, Size
        );

        // MountainGenerator mountainGenerator = new(Amount, mountainData);

        float isolationDirectionProb = mountainData.GetProbability(MountainData.MountainStat.ISOLATION_DIRECTION, num);

        mountainData.PrintCDF(MountainData.MountainStat.ISOLATION_DIRECTION);

        mountainData.PrintHistogram(MountainData.MountainStat.ISOLATION_DIRECTION);

        // GD.Print(isolationDirectionProb);
    }
    public void Run()
    {
        Lines.Multimesh = new()
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            UseColors = true,
            Mesh = new Func<Mesh>(() =>
            {
                ImmediateMesh immediateMesh = new();
                immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, new StandardMaterial3D());
                immediateMesh.SurfaceAddVertex(Vector3.Zero);
                immediateMesh.SurfaceAddVertex(Vector3.Up);
                immediateMesh.SurfaceEnd();
                immediateMesh.SurfaceSetMaterial(0, LineMaterial);
                return immediateMesh;
            }).Invoke()
        };

        Display.Mesh = new PlaneMesh();
        ((PlaneMesh)Display.Mesh).Size = 2 * ViewSize;
        ((PlaneMesh)Display.Mesh).SubdivideWidth = 512;
        ((PlaneMesh)Display.Mesh).SubdivideDepth = 512;
        ((PlaneMesh)Display.Mesh).Material = DisplayMaterial;

        Image coarseElevationMap = CoarseElevationMap.GetImage();
        Image peakDensityMap = PeakDensityMap.GetImage();

        if (coarseElevationMap.IsCompressed())
            coarseElevationMap.Decompress();
        if (peakDensityMap.IsCompressed())
            peakDensityMap.Decompress();

        Display.Position = Vector3.Zero;

        MountainGenerator mountainGenerator = new(Amount, new(new Dictionary<MountainData.MountainStat, (float[] samples, int binCount)>
        {
            {MountainData.MountainStat.ELEVATION, (CurveToArray(ElevationCurve, 1024), 20)},
            {MountainData.MountainStat.ISOLATION_DISTANCE, (CurveToArray(IsolationDistanceCurve, 1024), 20)},
            {MountainData.MountainStat.ISOLATION_DIRECTION, (CurveToArray(IsolationDirectionCurve, 1024), 12)},
            {MountainData.MountainStat.PROMINENCE, (CurveToArray(ProminenceCurve, 1024), 20)},
            {MountainData.MountainStat.DOMINANCE_GROUP, (CurveToArray(DominanceGroupsCurve, 1024), 12)}
        },
            coarseElevationMap, peakDensityMap, Size
        ));

        ImageTexture test = ImageTexture.CreateFromImage(mountainGenerator.Test);
        ((ShaderMaterial)DisplayMaterial).SetShaderParameter("coarse_elevation_map", CoarseElevationMap);
        ((ShaderMaterial)DisplayMaterial).SetShaderParameter("peak_density_map", PeakDensityMap);
        ((ShaderMaterial)DisplayMaterial).SetShaderParameter("bump_map", test);
        ((ShaderMaterial)DisplayMaterial).SetShaderParameter("height_scale", HeightScale);
        // mountainData.PrintHistogram(MountainData.MountainStat.ELEVATION);
        // mountainData.PrintHistogram(MountainData.MountainStat.ISOLATION_DISTANCE);
        // mountainData.PrintHistogram(MountainData.MountainStat.ISOLATION_DIRECTION);

        DrawPeaksAndSaddles(mountainGenerator.peaks);
        // DrawDivideTreeEdges(mountainGenerator.DivideTree);
    }

    // private void DrawDivideTreeEdges(DivideTree divideTree)
    // {
    //     var edges = divideTree.GetEdges().ToArray();
    //     Lines.Multimesh.InstanceCount = edges.Length;
    //     Vector3 offsetScale = new(Size.X, ElevationCurve.MaxValue, Size.Y);
    //     int lineInstanceCount = 0;

    //     foreach ((DivideTree.DivideTreeNode childNode, DivideTree.DivideTreeNode parentNode) in edges)
    //     {
    //         Vector3 child = CenterOrigin(childNode.Position / offsetScale, ViewSize);
    //         Vector3 parent = CenterOrigin(parentNode.Position / offsetScale, ViewSize);
    //         // GD.PrintS(child, childNode.Position);

    //         Vector3 direction = parent - child;

    //         Lines.Multimesh.SetInstanceTransform(lineInstanceCount, new Transform3D(Basis.Identity, child));
    //         Lines.Multimesh.SetInstanceCustomData(lineInstanceCount, new Color(direction.X, direction.Y, direction.Z));
    //         Lines.Multimesh.SetInstanceColor(lineInstanceCount, Colors.White);

    //         lineInstanceCount++;
    //     }
    // }

    public static float[] CurveToArray(Curve curve, int samples)
    {
        float[] results = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            results[i] = curve.Sample(t);
        }

        return results;
    }

    private static Vector3 CenterOrigin(Vector3 position, Vector3 size)
    {
        // GD.Print(position);
        position.X *= 2;
        position.Z *= 2;
        return size * (position - new Vector3(1, 0, 1));
    }

    private void DrawPeaksAndSaddles(List<Vector3> peaks)
    {
        GD.Print(peaks.Count);
        Vector3 offsetScale = new(Size.X, 1, Size.Y);

        // List<DivideTree.DivideTreeNode> nodes = divideTree.Nodes;

        Multimesh = new()
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new SphereMesh
            {
                Radius = PointRadius,
                Height = 2 * PointRadius,
                RadialSegments = 8,
                Rings = 4
            },
            // InstanceCount = nodes.Count + 8
            InstanceCount = peaks.Count
        };

       
        // Draw peaks
        // for (int i = 0; i < nodes.Count; i++)
        for (int i = 0; i < peaks.Count; i++)
        {
            // DivideTree.DivideTreeNode node = nodes[i];
            // Vector3 peak = CenterOrigin(peaks[i], new Vector3(Size.X, 1, Size.Y));


            Vector3 peak = peaks[i];
            Vector3 origin = peak / offsetScale;
            Vector3 position = CenterOrigin(origin, new Vector3(ViewSize.X, 1, ViewSize.Y));
            // GD.Print(peak);
            

            
            Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, position));

            // Future: set custom color or data
            // Multimesh.SetInstanceColor(i + 8, node.IsSaddle ? Colors.Gray : Colors.White);
        }
    }

}
