using Godot;

namespace PlanetGame.Planet.Rendering.Drawing.Brushes
{
    public class BasicBrush : Brush
    {
        public Color Color { get; set; } = Colors.White;

        public override Color Sample(Vector2 position)
        {
            float distance = position.Length();

            if (distance > 1.0f)
                return Colors.Transparent;

            return Colors.White;
        }
    }
}