using Godot;
namespace PlanetGame.Rendering.VirtualTexturing
{
    public abstract class VirtualTextureTable
    {
        protected Texture StorageTexture { get; set; }
        public Control Visualization { get; protected set; }

        protected abstract void CreateVisualization();
        public abstract void CleanupGPU();
        public abstract void ClearStorageTexture();
    }
}