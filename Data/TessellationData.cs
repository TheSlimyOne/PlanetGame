using Godot;

namespace PlanetGame.Data
{
    public class TessellationData : ISavable
    {
        public float Radius;
        public uint Resolution;
        public float HeightScale;
        public float SubFactor;
        public uint MaximumLod;
        public uint MinimumLod;
        public uint MaximumKeys;
        public float CullingDepth;
        public Vector4 CullingMargin;
        public TessellationData() { }

        public TessellationData(float radius, uint resolution, float heightScale, float subFactor, uint maximumLod, uint minimumLod, uint maximumKeys, float cullingDepth, Vector4 cullingMargin)
        {
            Radius = radius;
            Resolution = resolution;
            HeightScale = heightScale;
            SubFactor = subFactor;
            MaximumLod = maximumLod;
            MinimumLod = minimumLod;
            MaximumKeys = maximumKeys;
            CullingDepth = cullingDepth;
            CullingMargin = cullingMargin;
        }

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
            CullingMargin: {CullingMargin}
            """;
        }
    }
}