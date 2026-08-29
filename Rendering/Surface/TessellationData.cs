using Godot;

namespace PlanetGame.Rendering.Surface
{
    public class TessellationData
    {
        public float Radius;
        public uint Resolution;
        public float HeightScale;
        public float SubFactor;
        public uint MaximumLod;
        public uint MinimumLod;
        public uint MaximumKeys;
        public uint StartingLod;
        public float CullingDepth;
        public Vector4 CullingMargin;

        public TessellationData() { }

        public TessellationData(float radius, uint resolution, float heightScale, float subFactor, uint maximumLod, uint minimumLod, uint maximumKeys, uint startingLod, float cullingDepth, Vector4 cullingMargin)
        {
            Radius = radius;
            Resolution = resolution;
            HeightScale = heightScale;
            SubFactor = subFactor;
            MaximumLod = maximumLod;
            MinimumLod = minimumLod;
            MaximumKeys = maximumKeys;
            StartingLod = startingLod;
            CullingDepth = cullingDepth;
            CullingMargin = cullingMargin;
        }

        public uint GetStartingPrimitiveCount => (uint)(6 * Mathf.Pow(4, StartingLod + 1));

        public override string ToString()
        {
            return $"""
            Radius: {Radius}
            Resolution: {Resolution}
            HeightScale: {HeightScale}
            SubFactor: {SubFactor}
            MaximumLod: {MaximumLod}
            MinimumLod: {MinimumLod}
            MaximumKeys: {MaximumKeys}
            StartingLod: {StartingLod}
            CullingMargin: {CullingMargin}
            """;
        }
    }
}