namespace PlanetGame.Shaders
{
    public interface IGPUResource
    {
        public virtual int GetID()
        {
            return GetHashCode();
        }

        public const bool Verbose = false;
    }
}