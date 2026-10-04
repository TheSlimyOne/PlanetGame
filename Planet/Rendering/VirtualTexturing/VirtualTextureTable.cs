using Godot;
using PlanetGame.Shaders;
using Uniform;
namespace PlanetGame.Planet.Rendering.VirtualTexturing
{
    public abstract class VirtualTextureTable(string name, IGPUResource owner, RenderingDevice.UniformType format, bool perserved) : Texture2DUniform(name, owner, RenderingServer.GetRenderingDevice(), format, perserved: perserved)
    {
        protected Texture _storageTexture { get; set; }
        public TextureRect Visualization;
        public uint Size { get; protected set; }

        public abstract TextureRect CreateVisualization(string name);
        
        // public abstract void CleanupGPU();
        public abstract void ClearStorageTexture();
        public abstract void SetFallbackSlots();

        protected override void FreeRidInternal()
        {
            if (_storageTexture is Texture2Drd texture2Drd)
                texture2Drd.TextureRdRid = default;
            
            else if (_storageTexture is Texture2DArrayRD texture2DArrayRD)
                texture2DArrayRD.TextureRdRid = default;
            
            _storageTexture = default;
        }
    }
}