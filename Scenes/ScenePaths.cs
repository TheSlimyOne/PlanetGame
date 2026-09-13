using Godot;

namespace PlanetGame
{
    public static class ScenePaths
    {
        public const string DEBUG_SECTION = "res://Util/Debug Menu Utils/Scenes/section_component.tscn";
        public const string DEBUG_BUTTON = "res://Util/Debug Menu Utils/Scenes/button_component.tscn";
        public const string DEBUG_SLIDER = "res://Util/Debug Menu Utils/Scenes/slider_component.tscn";
        public const string DEBUG_TEXTURE = "res://Util/Debug Menu Utils/Scenes/texture_component.tscn";
        public const string DEBUG_DISTRIBUTION = "res://Util/Debug Menu Utils/Scenes/distribution_component.tscn";
        public const string DEBUG_LABEL = "res://Util/Debug Menu Utils/Scenes/label_component.tscn";

        public const string PLANET_DRAWING_CONTROLLER = "res://Planet/Rendering/Drawing/UI/planet_drawing_controller.tscn";
        public const string BRUSH_OPTIONS = "res://Planet/Rendering/Drawing/UI/brush_options.tscn";

        public static PackedScene GetPackedScene(string scenePath)
        {
            return GD.Load<PackedScene>(scenePath);
        }
        public static T InstantiateScene<T>(string scenePath) where T : Node
        {
            return GetPackedScene(scenePath).Instantiate<T>();
        }
    }
}