using Godot;
using PlanetGame.Util;

namespace PlanetGame.Data
{
    public class AtmosphereData(float scale) : ISavable
    {
        public float Scale = scale;
        public float DensityFalloff;
        public uint LightSamplingCount;
        public uint OpticalDepthSamplingCount;

        Vector3 WaveLengths = new(700, 530, 440);
        public float ScatteringStrength;


        public override string ToString()
        {
            return $"""
            Atmosphere Scale: {Scale}
            """;
        }

        /// <summary>
        /// Serializes this data to match the expected GPU buffer layout.
        /// </summary>
        /// <remarks>
        /// <code>
        /// layout(std430, binding = X) readonly buffer AtmosphereData {
        ///     float radius;
        /// };
        /// </code>
        /// </remarks>
        public byte[] ToBytes()
        {

            float scatterR = Mathf.Pow(400 / WaveLengths.X, 4) * ScatteringStrength;
            float scatterG = Mathf.Pow(400 / WaveLengths.Y, 4) * ScatteringStrength;
            float scatterB = Mathf.Pow(400 / WaveLengths.Z, 4) * ScatteringStrength;

            Vector3 scatteringCoefficients = new (scatterR, scatterG, scatterB);

            return [
                .. Utilities.ToBytesSingle(Scale),
                .. Utilities.ToBytesSingle(DensityFalloff),
                .. Utilities.ToBytesSingle(LightSamplingCount),
                .. Utilities.ToBytesSingle(OpticalDepthSamplingCount),

                .. Utilities.ToBytesSingle(VectorUtils.ToVector4(scatteringCoefficients, 1)),
                // .. Utilities.ToBytesSingle(ScatteringStrength),

            ];
        }
    }
}