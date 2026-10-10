using System;
using System.Collections.Generic;
using Godot;
using PlanetGame.Shaders.Dispatchers;
using Uniform;

namespace PlanetGame.Shaders
{
    // public interface IDispatchable : IGPUResource { }

    public abstract class ShaderPass<BufferId, SetId>(RenderingDevice renderingDevice, ShaderProgramPaths shaderPath)
        : IGPUResource
        where BufferId : struct, Enum
        where SetId : struct, Enum
    {
        public RenderingDevice RenderingDevice { get; private set; } = renderingDevice;
        protected ShaderProgramPaths _shaderProgramPaths = shaderPath;
        protected Dictionary<uint, Rid> _uniformSets = [];
        protected Rid _shader;
        protected Rid _pipeline;

        private Dictionary<BufferId, ShaderUniform> _shaderUniforms = [];
        private Dictionary<BufferId, (int binding, uint set)> _setMappings = [];

        protected ShaderPass(ShaderProgramPaths shaderPath) : this(RenderingServer.GetRenderingDevice(), shaderPath) { }

        public ShaderUniform this[BufferId bufferId]
        {
            get => GetUniform(bufferId);
        }

        public ShaderUniform this[int binding, BufferId bufferId, SetId setId]
        {
            set
            {
                _shaderUniforms[bufferId] = value;
                _setMappings[bufferId] = (binding, Convert.ToUInt32(setId));
            }
        }

        public ShaderUniform GetUniform(BufferId bufferId)
        {
            return _shaderUniforms[bufferId];
        }
        public T GetUniform<T>(BufferId bufferId) where T : ShaderUniform => (T)_shaderUniforms[bufferId];

        public virtual void UpdateUniforms() => throw new NotImplementedException();


#nullable enable
        public void Invoke(object[]? pushConstants = null)
        {
            if (!IsValid())
                return;

            InvokeInternal(pushConstants);
        }

        protected abstract void InvokeInternal(object[]? pushConstants = null);
#nullable disable

        public abstract void CreateUniforms();

        protected void CreateShader()
        {
            _shader = ShaderPaths.CompileShaderWithIncludes(_shaderProgramPaths, RenderingDevice);
        }

        protected virtual void CreatePipeline() => _pipeline = RenderingDevice.ComputePipelineCreate(_shader);

        protected void CreateUniformSet(SetId setId)
        {
            Godot.Collections.Array<RDUniform> bindings = [];

            uint set = Convert.ToUInt32(setId);
            foreach (BufferId bufferId in Enum.GetValues<BufferId>())
            {
                if (!_setMappings.TryGetValue(bufferId, out var mapping) || mapping.set != set)
                    continue;

                ShaderUniform shaderUniform = _shaderUniforms[bufferId];

                bindings.Add(shaderUniform.CreateRDUniform(mapping.binding));
            }

            if (_uniformSets.TryGetValue(set, out Rid uniformSet) && RenderingDevice.UniformSetIsValid(uniformSet))
                RenderingDevice.FreeRid(uniformSet);

            _uniformSets[set] = RenderingDevice.UniformSetCreate(bindings, _shader, set);
        }

        protected void CreateUniformSets()
        {
            foreach (SetId setId in Enum.GetValues<SetId>())
            {
                CreateUniformSet(setId);
            }
        }

        protected void FreePipeline()
        {
            if (RenderingDevice == null || !_pipeline.IsValid)
                return;

            if (RenderingDevice.ComputePipelineIsValid(_pipeline) || RenderingDevice.RenderPipelineIsValid(_pipeline))
                RenderingDevice.FreeRid(_pipeline);

            _pipeline = default;
        }

        public abstract bool IsValid();

        public bool UniformSetsIsValid()
        {
            foreach (SetId setId in Enum.GetValues<SetId>())
            {
                uint set = Convert.ToUInt32(setId);
                if (!RenderingDevice.UniformSetIsValid(_uniformSets[set]))
                    return false;
            }
            return true;
        }

        protected virtual void CleanupGPUInternal()
        {
            if (RenderingDevice == null)
                return;

            foreach ((uint _, Rid uniformSet) in _uniformSets)
            {
                if (RenderingDevice.UniformSetIsValid(uniformSet))
                    RenderingDevice.FreeRid(uniformSet);
            }
            _uniformSets.Clear();

            FreePipeline();

            if (_shader.IsValid)
                RenderingDevice.FreeRid(_shader);

            if (_shaderUniforms != null)
            {
                foreach (KeyValuePair<BufferId, ShaderUniform> kvp in _shaderUniforms)
                {
                    BufferId uniformName = kvp.Key;
                    ShaderUniform shaderUniform = kvp.Value;

                    string ownerName = shaderUniform.Owner is IGPUResource gpuOwner
                        ? $"{gpuOwner.GetType().Name} ID: {gpuOwner.GetID()}"
                        : shaderUniform.Owner?.GetType().Name ?? "NULL";

                    if (IGPUResource.Verbose)
                    {
                        GD.Print();
                        GD.Print($"Clearing {uniformName}");
                        GD.Print($"RID: {shaderUniform.Rid}");
                        GD.Print($"Container: {GetType().Name} ID: {GetID()}");
                        GD.Print($"Owner: {ownerName}");
                        GD.Print($"Preserved: {shaderUniform.Perserved}");
                    }

                    if (shaderUniform.Owner == this)
                    {
                        if (IGPUResource.Verbose)
                            GD.Print(shaderUniform.Perserved ? "RID preserved" : "Freeing RID");

                        shaderUniform.FreeRid();
                    }
                    else if (IGPUResource.Verbose)
                    {
                        GD.Print($"{GetType().Name} does not own this uniform container. Not calling FreeRid()");
                    }
                }

                _shaderUniforms.Clear();
            }

            _shaderUniforms = null;
            RenderingDevice = null;
        }
        public void CleanupGPU()
        {
           CleanupGPUInternal();
        }

        public override string ToString() =>
            $"{GetType().Name} (Shader RID: {_shader}, Pipeline RID: {_pipeline}, Uniform Set RID: {_uniformSets})";

        public int GetID() => GetHashCode();

        public override int GetHashCode()
        {
            HashCode hash = new();

            foreach (BufferId value in Enum.GetValues(typeof(BufferId)))
            {
                // Combine the enum value and ordinal position into the hash
                hash.Add(value.GetHashCode());
                hash.Add(Enum.GetNames<BufferId>()[value.GetHashCode()]);
            }

            return hash.ToHashCode();
        }
    }
}