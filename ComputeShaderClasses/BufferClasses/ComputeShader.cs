using System;
using Godot;
using Godot.Collections;
using System.Collections.Generic;
using ComputeShaderClasses;

namespace BufferClasses;

public abstract partial class ComputeShader<TEnum> : GodotObject where TEnum : Enum
{
    protected RenderingDevice _rd;
    protected String _shaderFilePath;
	protected Rid _uniformSet;
	protected Rid _shader;
	protected Rid _pipeline;

	protected Array<RDUniform> _bindings = new();
    protected System.Collections.Generic.Dictionary<TEnum, ComputeShaderUniform> _computeShaderUniforms;
    
    protected ComputeShader(string shaderFilePath, RenderingDevice rd = null) 
    {
        _shaderFilePath = shaderFilePath;
        _rd = rd ?? RenderingServer.GetRenderingDevice();
    }

    public ComputeShaderUniform GetUniform(TEnum @enum)
    {
        return _computeShaderUniforms[@enum];
    }
    public T GetUniform<T>(TEnum @enum) where T : ComputeShaderUniform
    {
        return (T)_computeShaderUniforms[@enum];
    }
    public T[] GetUniformData<T>(TEnum @enum) where T : unmanaged
    {
        return GetUniform(@enum).GetData<T>();
    }
    public byte[] GetUniformByteData(TEnum @enum)
    {
        return GetUniform(@enum).GetByteData();
    }
    public Rid GetUniformRid(TEnum @enum)
    {
        return GetUniform(@enum).Rid;
    }

    public abstract void UpdateUniforms();
    public abstract void Run();
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

    private Rid CreatePipeline(Rid shader)
	{
		return _rd.ComputePipelineCreate(shader);
	}

    public void Submit()
    {
        _rd.Submit();
    }
    public void Sync()
    {
        _rd.Sync();
    }
    public void SubmitThenSync()
    {
       _rd.Submit();
       _rd.Sync();
    }

    public void CleanupGPU()
	{
		if (_rd == null) return;

		foreach (ComputeShaderUniform computeShaderUniform in _computeShaderUniforms.Values)
		{
			computeShaderUniform.FreeRid();
		}

		_rd.FreeRid(_uniformSet);
		_rd.FreeRid(_pipeline);
		_rd.FreeRid(_shader);

		_rd.Free();
		_rd = null;
	}

}