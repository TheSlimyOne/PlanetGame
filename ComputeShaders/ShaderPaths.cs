namespace PlanetGame.ComputeShaders
{
    public static class ShaderPaths
    {
        public const string RENDER_SURFACE_PATH = "res://ComputeShaders/GLSL/ComputeCull.glsl";
        public const string COPY_KEYS_PATH = "res://ComputeShaders/GLSL/Copy.glsl";

        public const string CREATE_CUBE_MAP = "res://ComputeShaders/GLSL/ComputeCubeMap.glsl";
        public const string READ_FRAME_BUFFER = "res://ComputeShaders/GLSL/ReadFrameBuffer.glsl";
        public const string VALIDATE_TILE_CACHE = "res://ComputeShaders/GLSL/ValidateTileCache.glsl";

        public const string ARRAY_TEXTURE_VISUALIZER = "res://Assets/Shaders/Planet/array_texture_visualizer.gdshader";

        public const string SURFACE_SHADER_PATH = "res://Assets/Shaders/Planet/PlanetTerrainRenderSurfaceShader.gdshader";
        public const string FRAME_BUFFER_SHADER = "res://Assets/Shaders/Planet/PlanetTerrainFramebufferShader.gdshader";

        public const string DEMO_SHADER_PATH = "res://Assets/Shaders/Menu/demo_planet_shader.gdshader";
    }
}