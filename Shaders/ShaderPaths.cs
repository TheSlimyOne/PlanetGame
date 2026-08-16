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
        public const string EXECUTE_TESSELLATION_PASS = "res://Shaders/GLSL/ExecuteTessellationPass.compute";
        public const string PREPARE_TESSELLATION_PASS = "res://Shaders/GLSL/PrepareTessellationPass.compute";
        public const string RESOLVE_TILE_REQUEST_PASS = "res://Shaders/GLSL/ResolveTileRequestPass.compute";
        public const string PLANET_TESSELLATION_VERTEX = "res://Shaders/GLSL/PlanetTessellation.vertex";
        public const string PLANET_TESSELLATION_REQUEST_FRAGMENT = "res://Shaders/GLSL/PlanetTessellationRequest.fragment";
        public const string VALIDATE_TILE_CACHE = "res://Shaders/GLSL/ValidateTileCache.compute";
        public const string FLATTEN_INDIRECTION_TABLE = "res://Shaders/GLSL/FlattenIndirectionTable.compute";
        public const string EMPTY_FRAGMENT = "res://Shaders/GLSL/empty.fragment";
        
        // Gdshaders
        public const string GD_PLANET_TESSELLATION_PATH = "res://Assets/Shaders/Planet/planet_fragment.gdshader";
        public const string GD_DEMO_SHADER_PATH = "res://Assets/Shaders/Menu/demo_planet_shader.gdshader";


    }
}