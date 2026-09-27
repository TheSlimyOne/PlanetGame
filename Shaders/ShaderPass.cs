using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlanetGame.Shaders.Dispatchers;
using Uniform;

namespace PlanetGame.Shaders
{
    // public interface IDispatchable : IGPUResource { }

    public abstract class ShaderPass<TEnum>(RenderingDevice renderingDevice, ShaderProgramPaths shaderPath) : IGPUResource where TEnum : Enum
    {
        public RenderingDevice RenderingDevice { get; private set; } = renderingDevice;
        protected ShaderProgramPaths _shaderProgramPaths = shaderPath;
        protected Rid _uniformSet;
        protected Rid _shader;
        protected Rid _pipeline;

        protected System.Collections.Generic.Dictionary<Enum, ShaderUniform> _shaderUniforms;

        protected ShaderPass(ShaderProgramPaths shaderPath) : this(RenderingServer.GetRenderingDevice(), shaderPath) { }

        public ShaderUniform this[Enum @enum]
        {
            get => GetUniform(@enum);
        }

        public ShaderUniform GetUniform(Enum @enum) => _shaderUniforms[@enum];
        public T GetUniform<T>(Enum @enum) where T : ShaderUniform => (T)_shaderUniforms[@enum];

        public virtual void UpdateUniforms() { }

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

        protected void CreateUniformSet()
        {
            Array<RDUniform> bindings = [];
            for (int bindingIndex = 0; bindingIndex < _shaderUniforms.Count; bindingIndex++)
            {
                TEnum @enum = (TEnum)Enum.ToObject(typeof(TEnum), bindingIndex);
                ShaderUniform shaderUniform = _shaderUniforms[@enum];
                RDUniform uniform = shaderUniform.CreateRDUniform(bindingIndex);
                bindings.Add(uniform);
            }

            // if (RenderingDevice.UniformSetIsValid(_uniformSet))
            // {                
            //     RenderingDevice.FreeRid(_uniformSet);
            // }
            _uniformSet = RenderingDevice.UniformSetCreate(bindings, _shader, 0);
        }

        public abstract bool IsValid();
      
        public virtual void CleanupGPU()
        {
            if (RenderingDevice == null)
                return;

            if (RenderingDevice.UniformSetIsValid(_uniformSet))
                RenderingDevice.FreeRid(_uniformSet);

            if (RenderingDevice.ComputePipelineIsValid(_pipeline))
                RenderingDevice.FreeRid(_pipeline);

            if (_shader.IsValid)
                RenderingDevice.FreeRid(_shader);

            if (_shaderUniforms != null)
            {
                foreach (KeyValuePair<Enum, ShaderUniform> kvp in _shaderUniforms)
                {
                    Enum uniformName = kvp.Key;
                    ShaderUniform shaderUniform = kvp.Value;

                    if (IGPUResource.Verbose)
                        GD.Print("========================");

                    if (IGPUResource.Verbose)
                        GD.Print($"Clearing {uniformName} in {GetType().Name} ID: {GetID()} Owner: {shaderUniform.Owner}");

                    if (shaderUniform.Owner == this)
                    {
                        if (IGPUResource.Verbose)
                            GD.Print(shaderUniform.Rid);

                        shaderUniform.FreeRid();
                    }
                    else if (IGPUResource.Verbose)
                    {
                        GD.Print($"{GetType().Name} does not own this uniform. Not free rid");
                    }

                    if (IGPUResource.Verbose)
                        GD.Print("========================");
                }
            }

            _shaderUniforms = null;
            RenderingDevice = null;
        }

        public int GetID() => GetHashCode();

        public override int GetHashCode()
        {
            HashCode hash = new();

            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                // Combine the enum value and ordinal position into the hash
                hash.Add(value.GetHashCode());
                hash.Add(Enum.GetNames(typeof(TEnum))[value.GetHashCode()]);
            }

            return hash.ToHashCode();
        }
    }
}