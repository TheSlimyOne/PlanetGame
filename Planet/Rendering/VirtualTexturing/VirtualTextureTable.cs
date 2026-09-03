using Godot;
namespace PlanetGame.Rendering.VirtualTexturing
{
    public abstract class VirtualTextureTable
    {
        public RenderingDevice.DataFormat Format { get; protected set; }
        protected Texture _storageTexture { get; set; }
        public abstract Rid GetRdRid();
        public TextureRect Visualization;
        public abstract TextureRect CreateVisualization(string name);
        public virtual void DeleteVisualization()
        {
            if(Visualization != null)
            {
                Visualization.Free();
                // if (Visualization.Texture is Texture2Drd texture2Drd)
                //     texture2Drd.Free();

                // if (Visualization.Texture is ImageTexture imageTexture)
                //     imageTexture.Dispose();
            }
        }
        public abstract void CleanupGPU();
        public abstract void ClearStorageTexture();
        public abstract void SetFallbackSlots();
        public abstract Color GetPixel(int x, int y, int z);
    }
}