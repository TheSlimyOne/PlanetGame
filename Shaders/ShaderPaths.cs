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
        public const string RESOLVE_TILE_TEXTURE_PASS = "res://Shaders/GLSL/ResolveTileTexturePass.compute";
        public const string PLANET_TESSELLATION_VERTEX = "res://Shaders/GLSL/PlanetTessellation.vertex";
        public const string PLANET_TESSELLATION_REQUEST_FRAGMENT = "res://Shaders/GLSL/PlanetTessellationRequest.fragment";
        public const string VALIDATE_TILE_CACHE = "res://Shaders/GLSL/ValidateTileCache.compute";

        // Gdshaders
        public const string ARRAY_TEXTURE_VISUALIZER = "res://Assets/Shaders/Planet/array_texture_visualizer.gdshader";
        public const string TEXTURE_2D_ARRAY_SHADER = "res://Rendering/VirtualTexturing/Visualization Shaders/Texture2DArray.gdshader";
        public const string RESIDENCY_TABLE_SHADER = "res://Rendering/VirtualTexturing/Visualization Shaders/ResidencyShader.gdshader";
        public const string INDIRECTION_TABLE_SHADER = "res://Rendering/VirtualTexturing/Visualization Shaders/IndirectionShader.gdshader";

        public const string SURFACE_SHADER_PATH = "res://Assets/Shaders/Planet/PlanetTerrainRenderSurfaceShader.gdshader";
        public const string FRAME_BUFFER_SHADER = "res://Assets/Shaders/Planet/PlanetTerrainFramebufferShader.gdshader";

        public const string DEMO_SHADER_PATH = "res://Assets/Shaders/Menu/demo_planet_shader.gdshader";
    }
}