using PlanetGame.ComputeShaders.Dispatcher;

namespace PlanetGame.Rendering.Surface
{
    public class TerrainTessellator
    {
        public RenderSurfaceDispatcher RenderSurface { get; private set; }
        public CopyKeysDispatcher CopyKeys { get; private set; }

        public int CurrentLod;
        public int CulledCount;
        public int TotalCount;

        public TerrainTessellator(PlanetController planetController, MultiMeshRD planetMultiMesh, CustomCamera mainCamera, CustomCamera helperCamera)
        {
            RenderSurface = new();
            CopyKeys = new();

            RenderSurface.CopyKeysDispatcher = CopyKeys;
            RenderSurface.PlanetController = planetController;
            RenderSurface.MainCamera = mainCamera;
            RenderSurface.HelperCamera = helperCamera;
            RenderSurface.PlanetMultiMesh = planetMultiMesh;

            CopyKeys.RenderSurfaceDispatcher = RenderSurface;
            CopyKeys.PlanetMultimesh = planetMultiMesh;

            RenderSurface.CreateUniforms();
            CopyKeys.CreateUniforms();
        }

        public void CleanupGPUResources()
        {
            CopyKeys.CleanupGPU();
            RenderSurface.CleanupGPU();

            CopyKeys = null;
            RenderSurface = null;
        }

        public bool IsValidForProcessing()
        {
            return RenderSurface != null && CopyKeys != null;
        }

        public void Invoke()
        {
            if (!IsValidForProcessing())
                return;

            RenderSurface.ClearGlobalKeys();

            RenderSurface.Invoke();

            CurrentLod = RenderSurface.GetCurrentLod();

            CopyKeys.Invoke();

            RenderSurface.UpdateUniforms();

            (TotalCount, CulledCount) = RenderSurface.GetPrimitiveCounts();
        }
    }
}