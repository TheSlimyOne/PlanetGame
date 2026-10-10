using System;
using Godot;
using Uniform;
using System.Collections.Generic;
using PlanetGame.Planet.Rendering;
using static PlanetGame.Shaders.Dispatchers.PrepareTessellationPassDispatcher.BufferNames;
using static PlanetGame.Shaders.Dispatchers.PrepareTessellationPassDispatcher.BufferSets;

namespace PlanetGame.Shaders.Dispatchers
{
    public partial class PrepareTessellationPassDispatcher : Dispatcher<PrepareTessellationPassDispatcher.BufferNames, PrepareTessellationPassDispatcher.BufferSets>
    {
        private static ShaderProgramPaths _shaderPath = new() { Compute = ShaderPaths.PREPARE_TESSELLATION_COMPUTE };

        public enum BufferSets
        {
            DEFAULT = 0,
            MESH = 1
        }

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
        private Action _onMeshChange;

        public PrepareTessellationPassDispatcher(MultiMeshRD triangleMultiMesh, Dictionary<PlanetRenderer.BufferNames, ShaderUniform> sharedBufferRids) : base(_shaderPath)
        {
            _triangleMultiMesh = triangleMultiMesh;
            _sharedBufferRids = sharedBufferRids;
            SetupShader();

            _onMeshChange = () => CreateUniformSet(MESH);
            _triangleMultiMesh.BuffersChanged += _onMeshChange;
        }

        public override void CreateUniforms()
        {

            this[0, ATOMIC_COUNTER, DEFAULT] = _sharedBufferRids[PlanetRenderer.BufferNames.EXEC_ATOMIC_COUNTER];

            this[1, INDICES, DEFAULT] = _sharedBufferRids[PlanetRenderer.BufferNames.EXEC_KEY_INDICES];

            this[2, EXEC_DISPATCH_BUFFER, DEFAULT] = _sharedBufferRids[PlanetRenderer.BufferNames.EXEC_DISPATCH_BUFFER];

            this[3, DRAW_DISPATCH_BUFFER, DEFAULT] = _sharedBufferRids[PlanetRenderer.BufferNames.DRAW_DISPATCH_BUFFER];

            this[0, MULTIMESH_COMMAND_BUFFER, MESH] = _triangleMultiMesh.CommandBufferUniform;

            this[1, MESH_DATA, MESH] = _triangleMultiMesh.MeshDataUniform;

            CreateUniformSets();
        }

#nullable enable
        protected override void InvokeInternal(object[]? pushConstants = null)
        {
            long computeList = RenderingDevice.ComputeListBegin();
            RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
            ComputeListBindUniformSets(computeList);
            RenderingDevice.ComputeListAddBarrier(computeList);
            RenderingDevice.ComputeListDispatch(computeList, 1, 1, 1);
            RenderingDevice.ComputeListEnd();
        }
#nullable disable

        protected override void CleanupGPUInternal()
        {
            _triangleMultiMesh.BuffersChanged -= _onMeshChange;
            base.CleanupGPUInternal();
        }
    }
}
