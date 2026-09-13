using Godot;

namespace PlanetGame.Planet.Rendering.Drawing.Brushes
{
    public abstract class Brush
    {
        private float _hardness = 1.0f;
        private float _opacity = 1.0f;

        public float Size { get; set; } = 1.0f;

        public float Hardness
        {
            get => _hardness;
            set => _hardness = Mathf.Clamp(value / 100.0f, 0.0f, 1.0f);
        }

        public float Opacity
        {
            get => _opacity;
            set => _opacity = Mathf.Clamp(value / 100.0f, 0.0f, 1.0f);
        }

        public abstract Color Sample(Vector2 position);
    }
}