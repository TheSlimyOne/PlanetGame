using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PlanetGame.Planet;
using PlanetGame.Rendering.VirtualTexturing;
using PlanetGame.Shaders.Dispatchers;
using PlanetGame.Util;
using PlanetGame.Util.DebugUIComponents;
using Uniform;
using static PlanetGame.Planet.PlanetRenderer;

namespace PlanetGame.Rendering.Surface
{
    public class TerrainTessellator
    {
        private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;
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

        public MultiMeshRD TriangleMultiMesh { get; private set; }
        private Rid _planetInstance;

        public TerrainTessellator(Rid shader, Rid scenario, Dictionary<BufferNames, ShaderUniform> sharedUniforms)
        {
            SetupMultimesh(shader, scenario);
            ExecuteTessellationPass = new(TriangleMultiMesh, sharedUniforms);
            PrepareTessellationPass = new(TriangleMultiMesh, sharedUniforms);

            LodCounts = new int[TessellationData.MaximumLod + 1];

            BindTessellationDebugSettings();
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
            TriangleMultiMesh.CleanupGPU();


            PrepareTessellationPass = default;
            ExecuteTessellationPass = default;
            _planetInstance = default;
            TriangleMultiMesh = default;
        }

        public bool IsValidForProcessing()
        {
            return ExecuteTessellationPass?.IsValid() == true && PrepareTessellationPass?.IsValid() == true;
        }

        public void Invoke(CustomCamera camera)
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
                    camera.GetCullingViewProjectionMatrix(TessellationData.CullingMargin, TessellationData.CullingDepth),
                    camera.GlobalPosition,
                    Mathf.Tan(camera.GetCameraFov(true) / 2)
                )
            );

            PrepareTessellationPass.Invoke();

            ExecuteTessellationPass.UpdateUniforms();

            (TotalCount, CulledCount, RenderedCount) = ExecuteTessellationPass.GetPrimitiveCounts();

            (MaxLod, MinLod, StableCount, LodCounts) = ExecuteTessellationPass.GetGlobalKeyData();

            Ready = true;
        }

        public void SetupMultimesh(Rid shader, Rid scenario)
        {
            TriangleMultiMesh = new(
                (int)TessellationData.MaximumKeys,
                Key.GetTriangleMesh((int)TessellationData.Resolution),
                -1
            );

            _planetInstance = TriangleMultiMesh.CreateMultimeshInstance(
                Transform3D.Identity,
                shader,
                scenario,
                float.MaxValue,
                0b1u
            );
        }
        private void BindTessellationDebugSettings()
        {
            DebugMenuController.Instance.AddSection("Tessellation", 0, false, null, 200);

            DebugMenuController.Instance.AddButton("Enable Tessellation", "Tessellation", () => !Paused, () => Paused = !Paused);

            DebugMenuController.Instance.AddSlider("Resolution", "Tessellation", () => TessellationData.Resolution, value =>
            {
                TessellationData.Resolution = value;
                TriangleMultiMesh.SetMesh(Key.GetTriangleMesh((int)TessellationData.Resolution));
            }, 2u, 17u, 1u);

            DebugMenuController.Instance.AddDistribution("Lods", "Tessellation",
                LodCounts.Select((_, lod) => new DistributionComponent.DistributionBinding<int>(
                    $"LOD {lod}",
                    () => LodCounts[lod]
                )).ToArray()
            );
        }
    }
}