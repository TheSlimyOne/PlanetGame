using Godot;
using PlanetGame.ComputeShaders.Dispatcher;
using System.Threading.Tasks;
using Godot.Collections;
using System.Threading;
using System;
using Planet;

namespace PlanetGame.Rendering.Surface
{
    public class TerrainTessellator
    {
        public RenderSurfaceDispatcher RenderSurface { get; private set; }
        public CopyKeysDispatcher CopyKeys { get; private set; }

        public TerrainTessellator(PlanetData planetData, MultiMeshRD planetMultiMesh, CustomCamera mainCamera, CustomCamera helperCamera)
        {
            RenderSurface = new();
            CopyKeys = new();

            RenderSurface.CopyKeysDispatcher = CopyKeys;
            RenderSurface.PlanetData = planetData;
            RenderSurface.MainCamera = mainCamera;
            RenderSurface.HelperCamera = helperCamera;
            RenderSurface.PlanetMultiMesh = planetMultiMesh;

            CopyKeys.RenderSurfaceDispatcher = RenderSurface;
            CopyKeys.PlanetData = planetData;
            CopyKeys.PlanetMultiMesh = planetMultiMesh;

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

            // PlanetController.UIController.SetCurrentLOD(RenderSurface.GetCurrentLod() - 1);

            CopyKeys.Invoke();

            RenderSurface.UpdateUniforms();
          
            // (int all, int culled) = RenderSurface.GetPrimitiveCounts();
            // PlanetController.UIController.SetLabelKeyCount(culled, all);
        }
    }
}