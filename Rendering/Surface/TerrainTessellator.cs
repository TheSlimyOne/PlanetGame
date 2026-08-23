using Godot;
using PlanetGame.Shaders.Dispatchers;
using PlanetGame.Util;

namespace PlanetGame.Rendering.Surface
{
    public class TerrainTessellator
    {
        public PlanetController PlanetController { get; private set; }
        public ExecuteTessellationPassDispatcher ExecuteTessellationPass { get; private set; }
        public PrepareTessellationPassDispatcher PrepareTessellationPass { get; private set; }
        public int MaxLod { get; private set; } 
        public int MinLod { get; private set; } 
        public int CulledCount { get; private set; }
        public int TotalCount { get; private set; }
        public int RenderedCount { get; private set; }
        public int StableCount { get; private set; }
        public int[] LodCounts { get; private set; } = [];
        public bool Ready { get; private set; } = true;
        public bool Paused = false;
        public bool IsStable { get; private set; } = false;

        public TerrainTessellator(PlanetController planetController)
        {
            PlanetController = planetController;
            ExecuteTessellationPass = new();
            PrepareTessellationPass = new();

            ExecuteTessellationPass.PrepareTessellationPass = PrepareTessellationPass;
            ExecuteTessellationPass.PlanetController = planetController;

            PrepareTessellationPass.ExecuteTessellationPass = ExecuteTessellationPass;
            PrepareTessellationPass.PlanetController = planetController;
        }

        public void CreateUniforms()
        {
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
            ExecuteTessellationPass.ResetGlobalKeyData();

            ExecuteTessellationPass.Invoke(
                Utilities.ToViewPushConstants(
                    PlanetController.MainCamera.GetViewProjectionMatrix(),
                    PlanetController.MainCamera.GlobalPosition,
                    Mathf.Tan(PlanetController.MainCamera.GetCameraFov(true) / 2)
                )
            );

            PrepareTessellationPass.Invoke();

            ExecuteTessellationPass.UpdateUniforms();

            (TotalCount, CulledCount, RenderedCount) = ExecuteTessellationPass.GetPrimitiveCounts();

            (MaxLod, MinLod, StableCount, LodCounts) = ExecuteTessellationPass.GetGlobalKeyData();

            Ready = true;
        }
    }
}