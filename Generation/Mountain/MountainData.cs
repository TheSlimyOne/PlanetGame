using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PlanetGame.Util;

public class MountainData
{
    public record SampleDistribution
    (
        float[] Samples,
        float Max,
        float Min,
        Dictionary<int, List<float>> Histogram,
        float[] CDF
    );

    public enum MountainStat
    {
        ELEVATION,
        ISOLATION_DISTANCE,
        ISOLATION_DIRECTION,
        PROMINENCE,
        DOMINANCE_GROUP
    }


    private static readonly Random _random = new();

    private readonly Dictionary<MountainStat, SampleDistribution> Data = [];

    public readonly Image PeakDensityMap;
    public readonly Image CoarseElevationMap;

    public readonly Vector2I Area;

    public MountainData(Dictionary<MountainStat, (float[] samples, int binCount)> rawData, Image coarseElevationMap, Image peakDensityMap, Vector2I area)
    {
        Area = area;

        CoarseElevationMap = coarseElevationMap;
        PeakDensityMap = peakDensityMap;

        CoarseElevationMap.Resize(Area.X, Area.Y);
        PeakDensityMap.Resize(Area.X, Area.Y);


        foreach (var (from, (samples, binCount)) in rawData)
        {
            Dictionary<int, List<float>> histogram = ToHistogram(samples, binCount);
            float[] cdf = ToCDF(histogram);
            Data[from] = new(samples, samples.Max(), samples.Min(), histogram, cdf);
        }
    }

    public SampleDistribution this[MountainStat from]
    {
        get => Data[from];
    }

    public float ComputeAcceptanceProbability(Vector3 candidate, Vector2 sampleRegionSize)
    {
        Vector2 candidate2D = new(candidate.X, candidate.Z);

        float peakDensity = GetPeakDensity(candidate2D, sampleRegionSize);
        float expectedElevation = GetExpectedElevation(candidate2D, sampleRegionSize);

        if (peakDensity <= 0.01f)
            return 0;

        float normalizedElevation = (candidate.Y - this[MountainStat.ELEVATION].Min) / (this[MountainStat.ELEVATION].Max - this[MountainStat.ELEVATION].Min);
        float elevationDelta = expectedElevation - normalizedElevation;
        float elevationSigma = 0.05f * (this[MountainStat.ELEVATION].Max - this[MountainStat.ELEVATION].Min);
        float elevationWeight = Mathf.Exp(-elevationDelta * elevationDelta / (2f * elevationSigma * elevationSigma));

        return peakDensity * elevationWeight;
    }

    public float GetPeakDensity(Vector2 position, Vector2 sampleRegionSize)
    {
        float u = position.X / sampleRegionSize.X;
        float v = position.Y / sampleRegionSize.Y;
        return Sampler.SampleBilinear(PeakDensityMap, u, v).R;
    }

    public float GetExpectedElevation(Vector2 position, Vector2 sampleRegionSize)
    {
        float u = position.X / sampleRegionSize.X;
        float v = position.Y / sampleRegionSize.Y;
        return Sampler.SampleBilinear(CoarseElevationMap, u, v).R;
    }

    public SampleDistribution GetSampleDistribution(MountainStat from)
    {
        if (!Data.TryGetValue(from, out SampleDistribution sampleDistribution))
            throw new ArgumentException($"Does not contain key {from}");
        return sampleDistribution;
    }

    public float GetMax(MountainStat from)
    {
        if (!Data.TryGetValue(from, out SampleDistribution sampleDistribution))
            throw new ArgumentException($"Does not contain key {from}");

        return sampleDistribution.Max;
    }
    public float GetMin(MountainStat from)
    {
        if (!Data.TryGetValue(from, out SampleDistribution sampleDistribution))
            throw new ArgumentException($"Does not contain key {from}");

        return sampleDistribution.Min;
    }

    public float SampleCDF(MountainStat from, bool normalize = false)
    {
        if (!Data.TryGetValue(from, out SampleDistribution sampleDistribution))
            throw new ArgumentException($"Does not contain key {from}");

        float[] cdf = sampleDistribution.CDF;
        float minValue = GetMin(from);
        float maxValue = GetMax(from);

        if (minValue == maxValue)
            return minValue;

        float r = (float)_random.NextDouble();
        int binIndex = Array.FindIndex(cdf, x => x >= r);
        if (binIndex == -1)
            binIndex = cdf.Length - 1;

        int binCount = cdf.Length;
        float binSize = (maxValue - minValue) / binCount;

        float binMin = minValue + binIndex * binSize;
        float binMax = binMin + binSize;

        float value = binMin + (float)_random.NextDouble() * (binMax - binMin);

        if (normalize)
            return (value - minValue) / (maxValue - minValue);

        return value;
    }

    // public readonly float[] _dominanceGroups;
    // public readonly float[] _prominence;

    public float GetProbability(MountainStat from, float value)
    {
        if (!Data.TryGetValue(from, out SampleDistribution sampleDistribution))
            throw new ArgumentException($"Does not contain key {from}");

        float min = GetMin(from);
        float max = GetMax(from);

        Dictionary<int, List<float>> histogram = sampleDistribution.Histogram;
        int binCount = histogram.Count;

        if (Mathf.IsEqualApprox(min, max))
            return Mathf.IsEqualApprox(value, min) ? 1f : 0f;

        float range = max - min;
        int binIndex = Mathf.Clamp((int)((value - min) / range * binCount), 0, binCount - 1);

        

        // Convert bin counts to probability mass
        float totalSamples = histogram.Values.Sum(list => list.Count);
        float countInBin = histogram.TryGetValue(binIndex, out List<float> list) ? list.Count : 0f;
        // GD.PrintS(min, max, value, totalSamples, countInBin, countInBin / totalSamples * 100);
        return countInBin / totalSamples;
    }


    public static float[] ToCDF(Dictionary<int, List<float>> histogram)
    {
        int binCount = histogram.Keys.Max() + 1;
        float[] cdf = new float[binCount];
        float total = histogram.Values.Sum(list => list.Count);

        float cumulative = 0f;
        for (int i = 0; i < binCount; i++)
        {
            if (histogram.TryGetValue(i, out var values))
                cumulative += values.Count / total;
            cdf[i] = cumulative;
        }

        return cdf;
    }

    public static Dictionary<int, List<float>> ToHistogram(float[] data, int binCount)
    {
        float min = data.Min();
        float max = data.Max();
        bool isFlat = min == max;

        Dictionary<int, List<float>> bins = new(binCount);
        for (int i = 0; i < binCount; i++)
            bins[i] = [];

        if (isFlat)
        {
            foreach (float value in data)
                bins[0].Add(value);
        }
        else
        {
            float invRange = binCount / (max - min);
            foreach (float value in data)
            {
                int binIndex = Math.Min((int)((value - min) * invRange), binCount - 1);
                bins[binIndex].Add(value);
            }
        }

        return bins;
    }

    public float[] SampleMultiple(MountainStat from, int sampleCount)
    {
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            samples[i] = SampleCDF(from);

        return samples;
    }

    public void PrintHistogram(MountainStat from, bool raw = false)
    {
        Dictionary<int, List<float>> histogram = Data[from].Histogram;
        GD.Print($"Histogram: {from}");
        for (int i = 0; i < histogram.Count; i++)
            if (raw)
                GD.Print($"{i}, {histogram[i].Count}, {(histogram[i].Count > 0 ? histogram[i].Average() : 0)}");
            else
                GD.Print($"Bin {i}: {histogram[i].Count} Samples, Average: {(histogram[i].Count > 0 ? histogram[i].Average() : "Nan")}");
    }

    public void PrintCDF(MountainStat from)
    {
        float[] cdf = Data[from].CDF;
        GD.Print($"CDF: {from}");
        for (int i = 0; i < cdf.Length; i++)
        {
            float probability = (i == 0) ? cdf[0] : cdf[i] - cdf[i - 1];
            GD.Print($"Bin {i}: Probability = {probability * 100:F1}");
        }
    }

}