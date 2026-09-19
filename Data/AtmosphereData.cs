using PlanetGame.Util;

namespace PlanetGame.Data
{
    public class AtmosphereData(float radius) : ISavable
    {
        public float Radius = radius;

        public override string ToString()
        {
            return $"""
            Atmosphere Radius: {Radius}
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
            return [
                .. Utilities.ToBytesSingle(Radius),
            ];
        }
    }
}