using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using Uniform;

namespace Dispatcher;



public abstract class ComputeShaderDispatcher<TEnum> : IDispatchable where TEnum : Enum
{
    public RenderingDevice _RenderingDevice { get; private set; }
    protected string _shaderFilePath;
    protected Rid _uniformSet;
    protected Rid _shader;
    protected Rid _pipeline;


    protected System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform> _computeShaderUniforms;

    protected ComputeShaderDispatcher(string shaderFilePath)
    {
        _shaderFilePath = shaderFilePath;
        _RenderingDevice = RenderingServer.GetRenderingDevice();
    }

    protected ComputeShaderDispatcher(string shaderFilePath, RenderingDevice rd)
    {
        _shaderFilePath = shaderFilePath;
        _RenderingDevice = rd;
    }

    public ComputeShaderUniform GetUniform(Enum @enum) => _computeShaderUniforms[@enum];
    public T GetUniform<T>(Enum @enum) where T : ComputeShaderUniform => (T)_computeShaderUniforms[@enum];
    public Rid GetUniformRid(Enum @enum) => GetUniform(@enum).Rid;


    public abstract void UpdateUniforms();
    public abstract void Invoke();
    public abstract void CreateUniforms();

    public void SetupComputeShader()
    {
        _shader = CreateShader(_shaderFilePath);
        _pipeline = CreatePipeline(_shader);
    }

    private Rid CreateShader(string path)
    {
        RDShaderFile shaderFile = GD.Load<RDShaderFile>(path);
        RDShaderSpirV spirV = shaderFile.GetSpirV();
        return _RenderingDevice.ShaderCreateFromSpirV(spirV);
    }

    protected virtual Rid CreatePipeline(Rid shader) => _RenderingDevice.ComputePipelineCreate(shader);

    protected void CreateUniformSet()
    {
        Array<RDUniform> bindings = [];
        for (int i = 0; i < _computeShaderUniforms.Count; i++)
        {
            TEnum @enum = (TEnum)Enum.ToObject(typeof(TEnum), i);
            ComputeShaderUniform computeShaderUniform = _computeShaderUniforms[@enum];
            if (computeShaderUniform.Owner != this)
                _computeShaderUniforms[@enum] = computeShaderUniform.RebindUniform(this, _RenderingDevice, i);

            bindings.Add(_computeShaderUniforms[@enum].Uniform);
        }

        _uniformSet = _RenderingDevice.UniformSetCreate(bindings, _shader, 0);
    }

    public void SubmitThenSync()
    {
        Submit();
        Sync();
    }

    public void Submit() => _RenderingDevice.Submit();
    public void Sync() => _RenderingDevice.Sync();

    bool verbose = false;
    public virtual void CleanupGPU()
    {
        if (_RenderingDevice == null) return;

        if (_RenderingDevice.UniformSetIsValid(_uniformSet))
            _RenderingDevice.FreeRid(_uniformSet);
        if (_RenderingDevice.ComputePipelineIsValid(_pipeline))
            _RenderingDevice.FreeRid(_pipeline);
        
        _RenderingDevice.FreeRid(_shader);
        
        foreach (KeyValuePair<Enum, ComputeShaderUniform> kvp in _computeShaderUniforms)
        {
            Enum uniformName = kvp.Key;
            ComputeShaderUniform computeShaderUniform = kvp.Value;

            if (verbose) GD.Print("========================");
            if (verbose) GD.Print($"Clearing {uniformName} in {GetType().Name} ID: {GetID()} Owner: {computeShaderUniform.Owner}");
            if (computeShaderUniform.Owner == this)
            {
                if (verbose) GD.Print(computeShaderUniform.Rid);
                computeShaderUniform.FreeRids();
            }
            else { if (verbose) GD.Print($"{GetType().Name} does not own this uniform. Not free rid"); }
            if (verbose) GD.Print("========================");
        }

        _RenderingDevice = null;
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