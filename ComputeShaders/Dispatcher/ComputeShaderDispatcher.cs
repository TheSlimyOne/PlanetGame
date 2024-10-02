using System;
using Godot;
using Godot.Collections;
using Uniform;

namespace Dispatcher;



public abstract class ComputeShaderDispatcher<TEnum> : IDispatchable where TEnum : Enum
{
    public RenderingDevice _rd;
    protected string _shaderFilePath;
    protected Rid _uniformSet;
    protected Rid _shader;
    protected Rid _pipeline;


    protected System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform> _computeShaderUniforms;

    protected ComputeShaderDispatcher(string shaderFilePath, ref RenderingDevice rd)
    {
        _shaderFilePath = shaderFilePath;
        _rd = rd;
    }

    public ComputeShaderUniform GetUniform(Enum @enum) => _computeShaderUniforms[@enum];
    public T GetUniform<T>(Enum @enum) where T : ComputeShaderUniform => (T)_computeShaderUniforms[@enum];
    public T[] GetUniformData<T>(Enum @enum) where T : unmanaged => ((StorageBufferUniform)GetUniform(@enum)).GetData<T>();
    public Rid GetUniformRid(Enum @enum) => GetUniform(@enum).Rid;
    

    public abstract void UpdateUniforms();
    public abstract void Ready();
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
        return _rd.ShaderCreateFromSpirV(spirV);
    }

    private Rid CreatePipeline(Rid shader) => _rd.ComputePipelineCreate(shader);
    
    protected void CreateUniformSet()
    {
        Array<RDUniform> bindings = new();
        for (int i = 0; i < _computeShaderUniforms.Count; i++)
        {
            TEnum @enum = (TEnum)Enum.ToObject(typeof(TEnum), i);

            ComputeShaderUniform computeShaderUniform = _computeShaderUniforms[@enum];
            if (computeShaderUniform.OwnerID != GetID())
                _computeShaderUniforms[@enum] = computeShaderUniform.RebindUniform(this, _rd, i);

            bindings.Add(_computeShaderUniforms[@enum].Uniform);
        }

        _uniformSet = _rd.UniformSetCreate(bindings, _shader, 0);
    }

    public void SubmitThenSync()
    {
        _rd.Submit();
        _rd.Sync();
    }

    public virtual void CleanupGPU()
    {
        if (_rd == null) return;

        _rd.FreeRid(_uniformSet);
        _rd.FreeRid(_pipeline);
        _rd.FreeRid(_shader);
        foreach (ComputeShaderUniform computeShaderUniform in _computeShaderUniforms.Values)
        {
            if (computeShaderUniform.OwnerID == GetID())
                computeShaderUniform.FreeRid();
        }

        _rd = null;
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