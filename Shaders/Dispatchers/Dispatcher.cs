using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using Uniform;

namespace PlanetGame.Shaders.Dispatchers;

public interface IDispatchable : IGPUResource { }

public abstract class Dispatcher<TEnum> : IDispatchable where TEnum : Enum
{
    public RenderingDevice RenderingDevice { get; private set; }
    protected ShaderProgramPaths _shaderProgramPaths;
    protected Rid _uniformSet;
    protected Rid _shader;
    protected Rid _pipeline;

    public string Error { get; private set; } = "";

    protected System.Collections.Generic.Dictionary<Enum, ShaderUniform> _computeShaderUniforms;

    protected Dispatcher(ShaderProgramPaths shaderPath) : this(RenderingServer.GetRenderingDevice(), shaderPath) { }

    protected Dispatcher(RenderingDevice renderingDevice, ShaderProgramPaths shaderPath)
    {
        RenderingDevice = renderingDevice;
        _shaderProgramPaths = shaderPath;
    }

    public ShaderUniform this[Enum @enum]
    {
        get => GetUniform(@enum);
    }

    public ShaderUniform GetUniform(Enum @enum) => _computeShaderUniforms[@enum];
    public T GetUniform<T>(Enum @enum) where T : ShaderUniform => (T)_computeShaderUniforms[@enum];

    // TODO maybe rename this to resetUniforms
    public abstract void UpdateUniforms();
    
    #nullable enable
    public abstract void Invoke(byte[]? pushConstants = null);
    
    public abstract void CreateUniforms();

    public void SetupShader()
    {
        _shader = CreateShader();
        _pipeline = CreatePipeline();
    }

    private Rid CreateShader()
    {
        if (_shaderProgramPaths.Compute != "")
        {
            RDShaderSource shaderSource = SaveManager.LoadComputeShaderWithIncludes(_shaderProgramPaths.Compute);
            RDShaderSpirV spirV = RenderingDevice.ShaderCompileSpirVFromSource(shaderSource);
            Rid compiledShader = RenderingDevice.ShaderCreateFromSpirV(spirV);

            if (!compiledShader.IsValid)
            {
                GD.PrintErr($"For shader: {_shaderProgramPaths.Compute}");

                Error = ShaderError.FormatError(shaderSource.SourceCompute, spirV.CompileErrorCompute);
                GD.PrintRich(Error);
                GD.PrintErr(spirV.CompileErrorCompute.StripEdges().Replace("ERROR: ", ""));
            }
            return compiledShader;
        }
        else
        {
            GD.PrintErr($"Undefined Shader:\n\tVertex: {_shaderProgramPaths.Vertex}\n\tFragment: {_shaderProgramPaths.Fragment}\n\tCompute {_shaderProgramPaths.Compute}");
            return new();
        }
    }

    protected virtual Rid CreatePipeline() => RenderingDevice.ComputePipelineCreate(_shader);

    protected void CreateUniformSet()
    {
        Array<RDUniform> bindings = [];
        for (int i = 0; i < _computeShaderUniforms.Count; i++)
        {
            TEnum @enum = (TEnum)Enum.ToObject(typeof(TEnum), i);
            ShaderUniform computeShaderUniform = _computeShaderUniforms[@enum];

            // GD.PrintS(GetType(), computeShaderUniform.Rid, computeShaderUniform.Owner == this);

            if (computeShaderUniform.Owner != this)
                _computeShaderUniforms[@enum] = computeShaderUniform.RebindUniform(this, RenderingDevice, i);

            bindings.Add(_computeShaderUniforms[@enum].Uniform);
        }

        _uniformSet = RenderingDevice.UniformSetCreate(bindings, _shader, 0);
    }

    public bool IsValid()
    {
        return Error == "";
    }

    public void SubmitThenSync()
    {
        Submit();
        Sync();
    }

    public void Submit()
    {
        if (RenderingServer.GetRenderingDevice() == RenderingDevice)
            throw new InvalidOperationException("Cannot submit on the main rendering device.");
        RenderingDevice.Submit();
    }

    public void Sync()
    {
        if (RenderingServer.GetRenderingDevice() == RenderingDevice)
            throw new InvalidOperationException("Cannot sync on the main rendering device.");
        RenderingDevice.Sync();
    }

    static public bool Verbose = false;
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

        if (_computeShaderUniforms != null)
        {
            foreach (KeyValuePair<Enum, ShaderUniform> kvp in _computeShaderUniforms)
            {
                Enum uniformName = kvp.Key;
                ShaderUniform computeShaderUniform = kvp.Value;

                if (Verbose) GD.Print("========================");
                if (Verbose) GD.Print($"Clearing {uniformName} in {GetType().Name} ID: {GetID()} Owner: {computeShaderUniform.Owner}");
                if (computeShaderUniform.Owner == this)
                {
                    if (Verbose) GD.Print(computeShaderUniform.Rid);
                    computeShaderUniform.FreeRids();
                }
                else if (Verbose) GD.Print($"{GetType().Name} does not own this uniform. Not free rid");
                if (Verbose) GD.Print("========================");
            }
        }

        _computeShaderUniforms = null;
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