using System;
using Godot;
using Uniform;
using PlanetGame.Util;
using PlanetGame.Rendering.VirtualTexturing;
using System.Collections.Generic;
using PlanetGame.Planet;

namespace PlanetGame.Shaders.Dispatchers
{
    public partial class PrepareTessellationPassDispatcher : Dispatcher<PrepareTessellationPassDispatcher.BufferNames>
    {
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.PREPARE_TESSELLATION_PASS };

        public enum BufferNames
        {
            ATOMIC_COUNTER,
            INDICES,
            EXEC_DISPATCH_BUFFER,
            DRAW_DISPATCH_BUFFER,
            MULTIMESH_COMMAND_BUFFER,
            MESH_DATA
        }

        private MultiMeshRD _triangleMultiMesh;
        private readonly Dictionary<PlanetRenderer.BufferNames, ShaderUniform> _sharedBufferRids;

        public PrepareTessellationPassDispatcher(MultiMeshRD triangleMultiMesh, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedBufferRids) : base(_shaderPath)
        {
            _triangleMultiMesh = triangleMultiMesh;
            _sharedBufferRids = sharedBufferRids;
            SetupShader();

            _triangleMultiMesh.BuffersChanged += CreateUniformSet;
        }

        public override void CreateUniforms()
        {
            _shaderUniforms = new Dictionary<Enum, ShaderUniform>();

            _shaderUniforms[BufferNames.ATOMIC_COUNTER] = _sharedBufferRids[PlanetRenderer.BufferNames.EXEC_ATOMIC_COUNTER];

            _shaderUniforms[BufferNames.INDICES] = _sharedBufferRids[PlanetRenderer.BufferNames.EXEC_KEY_INDICES];

            _shaderUniforms[BufferNames.EXEC_DISPATCH_BUFFER] = _sharedBufferRids[PlanetRenderer.BufferNames.EXEC_DISPATCH_BUFFER];

            _shaderUniforms[BufferNames.DRAW_DISPATCH_BUFFER] = _sharedBufferRids[PlanetRenderer.BufferNames.DRAW_DISPATCH_BUFFER];

            _shaderUniforms[BufferNames.MULTIMESH_COMMAND_BUFFER] = _triangleMultiMesh.CommandBufferUniform;

            _shaderUniforms[BufferNames.MESH_DATA] = _triangleMultiMesh.MeshDataUniform;

            CreateUniformSet();
        }

#nullable enable
        public override void Invoke(byte[]? pushConstants = null)
        {
            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, 1, 1, 1);
            RenderingDevice.ComputeListEnd();
        }

        public override void CleanupGPU()
        {
            _triangleMultiMesh.BuffersChanged -= CreateUniformSet;
            base.CleanupGPU();
        }

    }
}
