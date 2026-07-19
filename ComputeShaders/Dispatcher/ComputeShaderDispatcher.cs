using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Godot.Collections;
using Uniform;

namespace PlanetGame.ComputeShaders.Dispatcher;

public abstract class ComputeShaderDispatcher<TEnum> : IDispatchable where TEnum : Enum
{
    public RenderingDevice RenderingDevice { get; private set; }
    protected string _shaderFilePath;
    protected Rid _uniformSet;
    protected Rid _shader;
    protected Rid _pipeline;

    public string Error { get; private set; } = "";

    protected System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform> _computeShaderUniforms;

    protected ComputeShaderDispatcher(string shaderFilePath) : this(RenderingServer.GetRenderingDevice(), shaderFilePath) { }

    protected ComputeShaderDispatcher(RenderingDevice renderingDevice, string shaderFilePath)
    {
        RenderingDevice = renderingDevice;
        _shaderFilePath = shaderFilePath;
    }

    public ComputeShaderUniform this[Enum @enum]
    {
        get => GetUniform(@enum);
    }

    public ComputeShaderUniform GetUniform(Enum @enum) => _computeShaderUniforms[@enum];
    public T GetUniform<T>(Enum @enum) where T : ComputeShaderUniform => (T)_computeShaderUniforms[@enum];

    // TODO maybe rename this to resetUniforms
    public abstract void UpdateUniforms();
    public abstract void Invoke();
    public abstract void CreateUniforms();

    public void SetupComputeShader()
    {
        _shader = CreateShader();
        _pipeline = CreatePipeline(_shader);
    }

    private Rid CreateShader()
    {
        RDShaderSource shaderSource = LoadComputeWithIncludes(_shaderFilePath);
        RDShaderSpirV spirV = RenderingDevice.ShaderCompileSpirVFromSource(shaderSource);

        Rid compiledShader = RenderingDevice.ShaderCreateFromSpirV(spirV);
        if (!compiledShader.IsValid)
        {
            Error = ShaderError.FormatError(shaderSource.SourceCompute, spirV.CompileErrorCompute);
            GD.PrintErr(spirV.CompileErrorCompute.StripEdges().Replace("ERROR: ", ""));
            GD.Print("\n\n\n");
            GD.PrintRich(Error);
        }
        return compiledShader;
    }

    protected virtual Rid CreatePipeline(Rid shader) => RenderingDevice.ComputePipelineCreate(shader);

    protected void CreateUniformSet()
    {
        Array<RDUniform> bindings = [];
        for (int i = 0; i < _computeShaderUniforms.Count; i++)
        {
            TEnum @enum = (TEnum)Enum.ToObject(typeof(TEnum), i);
            ComputeShaderUniform computeShaderUniform = _computeShaderUniforms[@enum];

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
        if (RenderingDevice == null) return;

        if (RenderingDevice.UniformSetIsValid(_uniformSet))
            RenderingDevice.FreeRid(_uniformSet);
        if (RenderingDevice.ComputePipelineIsValid(_pipeline))
            RenderingDevice.FreeRid(_pipeline);
        RenderingDevice.FreeRid(_shader);

        foreach (KeyValuePair<Enum, ComputeShaderUniform> kvp in _computeShaderUniforms)
        {
            Enum uniformName = kvp.Key;
            ComputeShaderUniform computeShaderUniform = kvp.Value;

            if (Verbose) GD.Print("========================");
            if (Verbose) GD.Print($"Clearing {uniformName} in {GetType().Name} ID: {GetID()} Owner: {computeShaderUniform.Owner}");
            if (computeShaderUniform.Owner == this)
            {
                if (Verbose) GD.Print(computeShaderUniform.Rid);
                computeShaderUniform.FreeRids();
            }
            else { if (Verbose) GD.Print($"{GetType().Name} does not own this uniform. Not free rid"); }
            if (Verbose) GD.Print("========================");
        }

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

    public static RDShaderSource LoadComputeWithIncludes(string shaderPath)
    {
        string shaderSrc = FileAccess.GetFileAsString(shaderPath);
        string[] lines = shaderSrc.Split('\n');

        StringBuilder stringBuilder = new();

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("#[compute]"))
            {
                continue;
            }
            else if (lines[i].TrimStart().Contains("#[include]"))
            {
                string path = lines[i][11..];
                string includeSrc = FileAccess.GetFileAsString(path);
                stringBuilder.AppendLine("// --- begin include: " + path + " ---");
                stringBuilder.AppendLine(includeSrc);
                stringBuilder.AppendLine("// --- end include: " + path + " ---");
            }
            else
            {
                stringBuilder.AppendLine(lines[i]);
            }
        }

        return new RDShaderSource() { SourceCompute = stringBuilder.ToString(), Language = RenderingDevice.ShaderLanguage.Glsl };
    }
}