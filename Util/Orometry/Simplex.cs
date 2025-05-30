using Godot;

namespace PlanetGame.Util.Orometry
{
    public abstract class Simplex
    {
        public abstract float GetUpperValue();
        public abstract float GetLowerValue();
        public abstract float GetAverageValue();
        public abstract Vector3 GetCentroid();
    }
}