using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PlanetGame.Util;
using static MountainData.MountainStat;

public class MountainGenerator
{
    public DivideTree DivideTree = new();
    public List<Vector3> peaks = [];
    private static readonly Random _random = new();

    public MountainGenerator(int amount, MountainData mountainData)
    {
        // Peaks = [.. GeneratePeaks(amount, mountainData, area).OrderByDescending(x => x)];
        // int j = 0;
        // while (DivideTree.Nodes.Count < 10 && j < 10)
        // {
        //     j++;
        //     break;
        // }

        // DivideTree =
        peaks = Generate(amount, mountainData);
        // GD.Print($"Nodes: {DivideTree.Nodes.Count}, Edges: {DivideTree.GetEdges().Count()}");
    }

    float NormalPDF(float x, float sigma)
    {
        float a = 1f / (sigma * MathF.Sqrt(2f * MathF.PI));
        float b = -0.5f * (x / sigma) * (x / sigma);
        return a * MathF.Exp(b);
    }

    public Image Test;
    public void DrawToImage(int x, int y, float elevation, Vector2I sampleRegionSize)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < sampleRegionSize.X && ny >= 0 && ny < sampleRegionSize.Y)
                    Test.SetPixel(nx, ny, new(elevation, 0, 0));
            }
    }


    public List<Vector3> Generate(int peakCount, MountainData mountainData)
    {
        Test = Image.CreateEmpty(mountainData.CoarseElevationMap.GetSize().X, mountainData.CoarseElevationMap.GetSize().Y, false, Image.Format.Rgba8);
        Test.Fill(Colors.Black);
        DivideTree divideTree = new();
        Random rng = new(1207);
        Vector2I sampleRegionSize = mountainData.Area;
        bool[,] used = new bool[sampleRegionSize.X, sampleRegionSize.Y];

        (float[,] coarseElevationMap, float maxCE, float minCE) = Utilities.To2Darray(mountainData.CoarseElevationMap, true);
        (float[,] peakDensityMap, _, _) = Utilities.To2Darray(mountainData.PeakDensityMap, true);

        float[] elevations = new float[peakCount];
        for (int i = 0; i < peakCount; i++)
        {
            elevations[i] = mountainData.SampleCDF(ELEVATION);
        }
        Array.Sort(elevations, (a, b) => b.CompareTo(a));

        List<Vector3> placedPeaks = [];

        float rangeCH = maxCE - minCE;
        float sigma = rangeCH / 20f;

        float maxElevation = mountainData.GetMax(ELEVATION);
        float minElevation = mountainData.GetMin(ELEVATION);

        for (int i = 0; i < elevations.Length; i++)
        {
            float normalizedElevation = (elevations[i] - minElevation) / (maxElevation - minElevation);
            float h = Mathf.Clamp(normalizedElevation - minCE / (maxCE - minCE), 0, 1);

            int j = 0;

            while (true)
            {
                int x = rng.Next(sampleRegionSize.X);
                int y = rng.Next(sampleRegionSize.Y);

                float coarseElevationPoint = coarseElevationMap[x, y];
                float peakDensityPoint = peakDensityMap[x, y];

                float normalProb = NormalPDF(coarseElevationPoint - h, sigma);
                (float isolationDistance, float isolationDirection) = ComputeIsolationValues(x, y, elevations[i], placedPeaks);

                float isolationDistanceProb = mountainData.GetProbability(ISOLATION_DISTANCE, isolationDistance);
                float isolationDirectionProb = mountainData.GetProbability(ISOLATION_DIRECTION, Mathf.RadToDeg(isolationDirection));


                float probability = peakDensityPoint * normalProb * isolationDirectionProb;

                float randomValue = (float)rng.NextDouble();

                if (randomValue < probability && !used[x, y])
                {
                    // GD.PrintS(Mathf.RadToDeg(isolationDirection), isolationDirectionProb);
                    placedPeaks.Add(new(x, elevations[i], y));
                    DrawToImage(x, y, elevations[i], sampleRegionSize);

                    used[x, y] = true;
                    break;
                }
                else if (j++ > sampleRegionSize.X * sampleRegionSize.Y / 2)
                    break;

            }
        }
        // placedPeaks.ForEach(x => GD.Print(x));


        return placedPeaks;
    }

    (float isolationDistance, float isolationDirection) ComputeIsolationValues(int x, int y, float h, List<Vector3> placedPeaks)
    {
        float minDist = float.MaxValue;
        Vector2 candidate = new(x, y);
        Vector2 nearest = Vector2.Zero;

        foreach (Vector3 peak in placedPeaks)
        {
            if (peak.Y > h)
            {
                Vector2 peakPosition = new(peak.X, peak.Z);
                float dist = candidate.DistanceSquaredTo(peakPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = peakPosition;
                }
            }
        }

        float angleRadians = Mathf.Atan2(nearest.Y - candidate.Y, nearest.X - candidate.X);
        if (angleRadians < 0)
            angleRadians += 2 * MathF.PI;
    
        return (Mathf.Sqrt(minDist), angleRadians);
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
            if (group.Count == 0 || MathF.Abs(val - group[^1]) <= threshold)
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