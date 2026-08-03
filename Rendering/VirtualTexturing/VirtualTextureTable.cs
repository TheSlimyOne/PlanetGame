using Godot;
namespace PlanetGame.Rendering.VirtualTexturing
{
    public abstract class VirtualTextureTable
    {
        public RenderingDevice.DataFormat Format { get; protected set; }
        protected Texture StorageTexture { get; set; }
        public abstract Rid GetRdRid();
        public abstract Control CreateVisualization(string name);
        public abstract void CleanupGPU();
        public abstract void ClearStorageTexture();
        public abstract void SetFallbackSlots();
        public abstract Color GetPixel(int x, int y, int z);
    }
}