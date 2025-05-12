using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static MountainData.MountainStat;

public class MountainGenerator
{
    public DivideTree DivideTree = new();
    private static readonly Random _random = new();

    public MountainGenerator(int amount, Vector2 area, MountainData mountainData)
    {
        // Peaks = [.. GeneratePeaks(amount, mountainData, area).OrderByDescending(x => x)];
        int j = 0;
        while (DivideTree.Nodes.Count < 10 && j < 10)
        {
            DivideTree = Generate(amount, mountainData, area);
            j++;
        }
        GD.Print($"Nodes: {DivideTree.Nodes.Count}, Edges: {DivideTree.GetEdges().Count()}");
    }

    public DivideTree Generate(int peakCount, MountainData mountainData, Vector2 sampleRegionSize)
    {
        DivideTree divideTree = new();
        HashSet<Vector2> occupiedCells = [];

        float rootProminence = mountainData.SampleCDF(PROMINENCE);
        float rootElevation = rootProminence;

        Vector3 rootPosition = new(
            0.5f * sampleRegionSize.X,
            rootElevation,
            0.5f * sampleRegionSize.Y
        );

       
        
        return divideTree;
    }

    private static bool IsValid(Vector3 candidate, MountainData mountainData, Vector2 sampleRegionSize, float cellSize, float radius, List<Vector3> points, int[,] grid)
    {
        if (candidate.X >= 0 && candidate.X < sampleRegionSize.X && candidate.Z >= 0 && candidate.Z < sampleRegionSize.Y)
        {
            float acceptanceProbability = mountainData.ComputeAcceptanceProbability(candidate, sampleRegionSize);
            float prob = (float)_random.NextDouble();
            if (acceptanceProbability < prob)
                return false;

            int cellX = (int)(candidate.X / cellSize);
            int cellY = (int)(candidate.Z / cellSize);
            int offset = Mathf.CeilToInt(radius / cellSize);
            int searchStartX = Mathf.Max(0, cellX - offset);
            int searchEndX = Mathf.Min(cellX + offset, grid.GetLength(0) - 1);
            int searchStartY = Mathf.Max(0, cellY - offset);
            int searchEndY = Mathf.Min(cellY + offset, grid.GetLength(1) - 1);

            for (int x = searchStartX; x <= searchEndX; x++)
            {
                for (int y = searchStartY; y <= searchEndY; y++)
                {
                    int pointIndex = grid[x, y] - 1;
                    if (pointIndex != -1)
                    {
                        Vector2 candidate2D = new(candidate.X, candidate.Z);
                        Vector2 otherPoint2D = new(points[pointIndex].X, points[pointIndex].Z);
                        float squaredDistance = (candidate2D - otherPoint2D).LengthSquared();

                        if (squaredDistance < radius * radius)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
        return false;
    }

    public static float[] SampleExpression(Func<float, float> expression, int samples)
    {
        float[] results = new float[samples];
        for (int i = 0; i < samples; i++)
            results[i] = expression(i / (samples - 1.0f));

        return results;
    }

    public static List<float> CondenseCloseValues(List<float> values, float threshold)
    {
        List<float> result = [];
        List<float> group = [];

        foreach (float val in values.OrderBy(x => x))
        {
            if (group.Count == 0 || MathF.Abs(val - group.Last()) <= threshold)
            {
                group.Add(val);
            }
            else
            {
                result.Add(group.Average());
                group = [val];
            }
        }

        if (group.Count > 0)
            result.Add(group.Average());

        return result;
    }

}