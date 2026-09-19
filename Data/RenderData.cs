using PlanetGame.Util;

namespace PlanetGame.Data
{
    public class RenderData() : ISavable
    {
        public uint DebugFlags { get; set; }
        
        public bool IsCulling = false;
        public bool IsMorphing = false;
        public bool IsCube = false;
        public override string ToString()
        {
            return $"""
            Culling: {IsCulling}
            Morphing: {IsMorphing}
            Cube: {IsCube}
            """;
        }

        /// <summary>
        /// Serializes this data to match the expected GPU buffer layout.
        /// </summary>
        /// <remarks>
        /// <code>
        /// layout(std430, binding = X) readonly buffer RenderData {
        ///     uint debug_flags;
        /// };
        /// </code>
        /// </remarks>
        public byte[] ToBytes()
        {
            return
            [
                .. Utilities.ToBytesSingle(Utilities.ToBitFlags([
                    IsCulling,
                    IsMorphing,
                    IsCube,
            ]))];
        }
    }
}