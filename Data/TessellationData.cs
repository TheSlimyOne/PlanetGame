using System.Text.Json.Serialization;
using Godot;
using PlanetGame.Util;

namespace PlanetGame.Data
{
    public class TessellationData : ISavable
    {
        public uint Resolution;
        public float SubFactor;
        public uint MaximumLod;
        public uint MinimumLod;
        public uint MaximumKeys;
        public float CullingDepth;
        public Vector4 CullingMargin;

        [JsonIgnore]
        public float HeightOffset { get; set; } = 0;

        public TessellationData() { }

        public TessellationData(uint resolution, float subFactor, uint maximumLod, uint minimumLod, uint maximumKeys, float cullingDepth, Vector4 cullingMargin)
        {
            Resolution = resolution;
            SubFactor = subFactor;
            MaximumLod = maximumLod;
            MinimumLod = minimumLod;
            MaximumKeys = maximumKeys;
            CullingDepth = cullingDepth;
            CullingMargin = cullingMargin;
        }

        /// <summary>
        /// Serializes this data to match the expected GPU buffer layout.
        /// </summary>
        /// <remarks>
        /// <code>
        /// layout(std430, binding = X) readonly buffer TessellationData {
        ///     uint resolution;
        ///     float sub_factor;
        ///     uint maximum_lod;
        ///     uint minimum_lod;
        ///     float height_offset;
        /// };
        /// </code>
        /// </remarks>
        public byte[] ToBytes()
        {
            return [
                .. Utilities.ToBytesSingle(Resolution),
                .. Utilities.ToBytesSingle(SubFactor),
                .. Utilities.ToBytesSingle(MaximumLod),
                .. Utilities.ToBytesSingle(MinimumLod),

                .. Utilities.ToBytesSingle(HeightOffset),
            ];
        }

        public override string ToString()
        {
            return $"""
            Resolution: {Resolution}
            SubFactor: {SubFactor}
            MaximumLod: {MaximumLod}
            MinimumLod: {MinimumLod}
            MaximumKeys: {MaximumKeys}
            CullingMargin: {CullingMargin}
            """;
        }
    }
}