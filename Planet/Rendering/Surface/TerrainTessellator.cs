using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Data;
using PlanetGame.Shaders.Dispatchers;
using Uniform;
using static PlanetGame.Planet.Rendering.PlanetRenderer;

namespace PlanetGame.Rendering.Surface
{
    public class TerrainTessellator
    {
        private static TessellationData TessellationData => SaveManager.TessellationData;
        public ExecuteTessellationPassDispatcher ExecuteTessellationPass { get; private set; }
        public PrepareTessellationPassDispatcher PrepareTessellationPass { get; private set; }

        public int MaxLod { get; private set; }
        public int MinLod { get; private set; }
        private int _culledCount;
        private int _totalCount;

        public int CulledCount
        {
            get => _culledCount;
            private set
            {
                _culledCount = value;
                CulledMax = Math.Max(CulledMax, value);
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                _totalCount = value;
                TotalMax = Math.Max(TotalMax, value);
            }
        }

        public int TotalMax { get; private set; }
        public int CulledMax { get; private set; }
        
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
        }

        public void CreateUniforms()
        {
            ExecuteTessellationPass.CreateUniforms();
            PrepareTessellationPass.CreateUniforms();
        }

        public void CleanupGPUResources()
        {
            PrepareTessellationPass?.CleanupGPU();
            ExecuteTessellationPass?.CleanupGPU();
            TriangleMultiMesh?.CleanupGPU();


            PrepareTessellationPass = default;
            ExecuteTessellationPass = default;
            _planetInstance = default;
            TriangleMultiMesh = default;
        }

        private bool _planetInstanceVisible = false;

        public bool PlanetInstanceVisible
        {
            get => _planetInstanceVisible;
            set
            {
                _planetInstanceVisible = value;
                RenderingServer.InstanceSetVisible(_planetInstance, value);
            }
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

            ExecuteTessellationPass.Invoke([
                camera.GetCullingViewProjectionMatrix(TessellationData.CullingMargin, TessellationData.CullingDepth),
                camera.GlobalPosition,
                Mathf.Tan(camera.GetCameraFov(true) / 2)
            ]);

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
                Data.Key.GetTriangleMesh((int)TessellationData.Resolution),
                -1
            );

            // _planetInstance = TriangleMultiMesh.CreateMultimeshInstance(
            //     Transform3D.Identity,
            //     shader,
            //     scenario,
            //     float.MaxValue,
            //     0b1u
            // );
        }

    }
}