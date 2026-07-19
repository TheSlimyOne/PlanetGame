namespace PlanetGame.ComputeShaders
{
    public static class ShaderPaths
    {
        public const string EXECUTE_TESSELLATION_PASS = "res://ComputeShaders/GLSL/ExecuteTessellationPass.glsl";
        public const string PREPARE_TESSELLATION_PASS = "res://ComputeShaders/GLSL/PrepareTessellationPass.glsl";
        public const string RESOLVE_TILE_TEXTURE_PASS = "res://ComputeShaders/GLSL/ResolveTileTexturePass.glsl";

        public const string VALIDATE_TILE_CACHE = "res://ComputeShaders/GLSL/ValidateTileCache.glsl";

        public const string ARRAY_TEXTURE_VISUALIZER = "res://Assets/Shaders/Planet/array_texture_visualizer.gdshader";
        public const string RESIDENCY_TABLE_SHADER = "res://Rendering/VirtualTexturing/Visualization Shaders/ResidencyShader.gdshader";
        public const string INDIRECTION_TABLE_SHADER = "res://Rendering/VirtualTexturing/Visualization Shaders/IndirectionShader.gdshader";

        public const string SURFACE_SHADER_PATH = "res://Assets/Shaders/Planet/PlanetTerrainRenderSurfaceShader.gdshader";
        public const string FRAME_BUFFER_SHADER = "res://Assets/Shaders/Planet/PlanetTerrainFramebufferShader.gdshader";

        public const string DEMO_SHADER_PATH = "res://Assets/Shaders/Menu/demo_planet_shader.gdshader";
    }
}