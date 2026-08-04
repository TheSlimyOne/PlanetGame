using Godot;
using PlanetGame.Shaders.Dispatchers;

namespace PlanetGame.Rendering.Surface
{
    public class TerrainTessellator
    {
        public ExecuteTessellationPassDispatcher ExecuteTessellationPass { get; private set; }
        public PrepareTessellationPassDispatcher PrepareTessellationPass { get; private set; }
        public int CurrentLod { get; private set; } 
        public int CulledCount { get; private set; }
        public int TotalCount { get; private set; }
        public int RenderedCount { get; private set; }
        public bool Ready { get; private set; } = true;
        public bool Paused = false;

        public TerrainTessellator(PlanetController planetController, SaveManager.WorldSave worldSave, MultiMeshRD planetMultiMesh, CustomCamera mainCamera)
        {
            ExecuteTessellationPass = new();
            PrepareTessellationPass = new();

            ExecuteTessellationPass.PrepareTessellationPass = PrepareTessellationPass;
            ExecuteTessellationPass.PlanetController = planetController;
            ExecuteTessellationPass.MainCamera = mainCamera;
            ExecuteTessellationPass.PlanetMultiMesh = planetMultiMesh;
            ExecuteTessellationPass.worldSave = worldSave;

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
            if (!Ready || !IsValidForProcessing())
                return;

            else if (Paused)
            {
                ExecuteTessellationPass.UpdateUniforms();
                return;
            }

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