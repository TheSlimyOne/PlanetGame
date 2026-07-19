using Godot;
using PlanetGame.ComputeShaders.Dispatcher;

namespace PlanetGame.Rendering.Surface
{
    public class TerrainTessellator
    {
        public ExecuteTessellationPassDispatcher ExecuteTessellationPass { get; private set; }
        public PrepareTessellationPassDispatcher PrepareTessellationPass { get; private set; }

        public int CurrentLod;
        public int CulledCount;
        public int TotalCount;
        public int RenderedCount;

        public bool Ready { get; private set; } = true;
        public bool Paused = false;

        public TerrainTessellator(PlanetController planetController, MultiMeshRD planetMultiMesh, CustomCamera mainCamera, CustomCamera helperCamera)
        {
            ExecuteTessellationPass = new();
            PrepareTessellationPass = new();

            ExecuteTessellationPass.PrepareTessellationPass = PrepareTessellationPass;
            ExecuteTessellationPass.PlanetController = planetController;
            ExecuteTessellationPass.MainCamera = mainCamera;
            ExecuteTessellationPass.HelperCamera = helperCamera;
            ExecuteTessellationPass.PlanetMultiMesh = planetMultiMesh;

            PrepareTessellationPass.ExecuteTessellationPass = ExecuteTessellationPass;
            PrepareTessellationPass.PlanetMultimesh = planetMultiMesh;

            ExecuteTessellationPass.CreateUniforms();
            PrepareTessellationPass.CreateUniforms();
        }

        public void CleanupGPUResources()
        {
            PrepareTessellationPass.CleanupGPU();
            ExecuteTessellationPass.CleanupGPU();

            PrepareTessellationPass = null;
            ExecuteTessellationPass = null;
        }

        public bool IsValidForProcessing()
        {
            return ExecuteTessellationPass?.IsValid() == true && PrepareTessellationPass?.IsValid() == true;
        }

        public void Invoke()
        {
            if (!Ready || !IsValidForProcessing() || Paused)
                return;

            Ready = false;
            ExecuteTessellationPass.ClearGlobalKeyData();

            ExecuteTessellationPass.Invoke();

            CurrentLod = ExecuteTessellationPass.GetCurrentLod();

            PrepareTessellationPass.Invoke();

            ExecuteTessellationPass.UpdateUniforms();

            (TotalCount, CulledCount, RenderedCount) = ExecuteTessellationPass.GetPrimitiveCounts();
            Ready = true;
        }
    }
}