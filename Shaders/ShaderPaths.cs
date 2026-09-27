using System.Text;
using Godot;

namespace PlanetGame.Shaders
{
    public struct ShaderProgramPaths
    {
        public string Vertex;
        public string Fragment;
        public string Compute;
    }

    public static class ShaderPaths
    {
        public const string EXECUTE_TESSELLATION_COMPUTE = "res://Shaders/GLSL/ExecuteTessellationPass.compute";
        public const string PREPARE_TESSELLATION_COMPUTE = "res://Shaders/GLSL/PrepareTessellationPass.compute";
        public const string RESOLVE_TILE_REQUEST_COMPUTE = "res://Shaders/GLSL/ResolveTileRequestPass.compute";
        public const string PLANET_VERTEX = "res://Shaders/GLSL/Planet.vertex";
        public const string PLANET_FRAGMENT = "res://Shaders/GLSL/Planet.fragment";
        public const string VALIDATE_TILE_CACHE_COMPUTE = "res://Shaders/GLSL/ValidateTileCache.compute";
        public const string CONSOLIDATE_INDIRECTION_TABLE_COMPUTE = "res://Shaders/GLSL/ConsolidateIndirectionTable.compute";
        public const string ATMOSPHERE_COMPUTE = "res://Shaders/GLSL/Atmosphere.compute";
        public const string ATMOSPHERE_VERTEX = "res://Shaders/GLSL/Atmosphere.vertex";
        public const string ATMOSPHERE_FRAGMENT = "res://Shaders/GLSL/Atmosphere.fragment";
        public const string PLANET_TESSELLATION_REQUEST_FRAGMENT = "res://Shaders/GLSL/PlanetTessellationRequest.fragment";

        public const string EMPTY_FRAGMENT = "res://Shaders/GLSL/empty.fragment";
        public const string EMPTY_VERTEX = "res://Shaders/GLSL/empty.vertex";


        // Gdshaders
        public const string GD_PLANET_TESSELLATION_PATH = "res://Assets/Shaders/Planet/planet_fragment.gdshader";
        public const string GD_DEMO_SHADER_PATH = "res://Assets/Shaders/Menu/demo_planet_shader.gdshader";

        // public static RDShaderSource LoadGraphicsShaderWithIncludes(string vertexShaderPath, string fragmentShaderPath)

        private static StringBuilder LoadShaderWithIncludes(string shaderPath, string tag)
        {
            string shaderSrc = FileAccess.GetFileAsString(shaderPath);
            string[] lines = shaderSrc.Split('\n');
            StringBuilder stringBuilder = new();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(tag))
                {
                    continue;
                }
                else if (lines[i].TrimStart().Contains("#[include]"))
                {
                    string path = lines[i][11..].TrimEnd();
                    // TODO error handle this file open
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

            return stringBuilder;
        }

        public static Rid CompileShaderWithIncludes(ShaderProgramPaths shaderProgramPaths, RenderingDevice renderingDevice)
        {
            if (shaderProgramPaths.Vertex != null && shaderProgramPaths.Fragment != null && shaderProgramPaths.Compute == null)
            {
                StringBuilder vertexStringBuilder = LoadShaderWithIncludes(shaderProgramPaths.Vertex, "#[vertex]");
                StringBuilder fragmentStringBuilder = LoadShaderWithIncludes(shaderProgramPaths.Fragment, "#[fragment]");

                RDShaderSource shaderSource = new()
                {
                    SourceVertex = vertexStringBuilder.ToString(),
                    SourceFragment = fragmentStringBuilder.ToString(),
                    Language = RenderingDevice.ShaderLanguage.Glsl
                };

                RDShaderSpirV shaderSpirV = renderingDevice.ShaderCompileSpirVFromSource(shaderSource);
                Rid compiledShader = renderingDevice.ShaderCreateFromSpirV(shaderSpirV);

                if (!compiledShader.IsValid)
                {
                    if (shaderSpirV.CompileErrorVertex.Length > 0)
                    {
                        string vertexError = ShaderError.FormatError(shaderSource.SourceVertex, shaderSpirV.CompileErrorVertex);
                        GD.PrintRich(vertexError);
                        GD.PrintErr(shaderSpirV.CompileErrorVertex.StripEdges().Replace("ERROR: ", ""));
                    }

                    if (shaderSpirV.CompileErrorFragment.Length > 0)
                    {
                        string fragmentError = ShaderError.FormatError(shaderSource.SourceFragment, shaderSpirV.CompileErrorFragment);
                        GD.PrintRich(fragmentError);
                        GD.PrintErr(shaderSpirV.CompileErrorFragment.StripEdges().Replace("ERROR: ", ""));
                    }
                }

                return compiledShader;
            }
            else if (shaderProgramPaths.Compute != null && shaderProgramPaths.Vertex == null && shaderProgramPaths.Fragment == null)
            {
                StringBuilder computeStringBuilder = LoadShaderWithIncludes(shaderProgramPaths.Compute, "#[compute]");

                RDShaderSource shaderSource = new()
                {
                    SourceCompute = computeStringBuilder.ToString(),
                    Language = RenderingDevice.ShaderLanguage.Glsl
                };

                RDShaderSpirV shaderSpirV = renderingDevice.ShaderCompileSpirVFromSource(shaderSource);
                Rid compiledShader = renderingDevice.ShaderCreateFromSpirV(shaderSpirV);


                if (!compiledShader.IsValid)
                {
                    if (shaderSpirV.CompileErrorCompute.Length > 0)
                    {
                        string computeError = ShaderError.FormatError(shaderSource.SourceCompute, shaderSpirV.CompileErrorCompute);
                        GD.PrintRich(computeError);
                        GD.PrintErr(shaderSpirV.CompileErrorCompute.StripEdges().Replace("ERROR: ", ""));
                    }
                }

                return compiledShader;
            }
            else
            {
                throw new System.Exception($"Invalid shader:\n\tVertex: {shaderProgramPaths.Vertex}\n\tFragment: {shaderProgramPaths.Fragment}\n\tCompute {shaderProgramPaths.Compute}");
            }
        }
    }
}