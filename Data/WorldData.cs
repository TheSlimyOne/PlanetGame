using Godot;
namespace PlanetGame.Data
{
    public class WorldData
    {
        public WorldData() { }

        public Vector3 PlanetPosition { get; set; } = Vector3.Zero;
        public Vector3 PlanetRotation { get; set; } = Vector3.Zero;
        public Vector3 PlanetScale { get; set; } = Vector3.One;

        public Transform3D GetTranslationTransform()
        {
            return new Transform3D(Basis.Identity, PlanetPosition);
        }

        public Transform3D GetRotationTransform()
        {
            return new Transform3D(Basis.FromEuler(PlanetRotation), Vector3.Zero);
        }

        public Transform3D GetScaleTransform()
        {
            return new Transform3D(Basis.Identity.Scaled(PlanetScale), Vector3.Zero);
        }

        public Transform3D[] GetTransforms()
        {
            return
            [
                GetTranslationTransform(),
                GetRotationTransform(),
                GetScaleTransform()
            ];
        }

        public override string ToString()
        {
            return $"""
            PlanetPosition: {PlanetPosition}
            PlanetRotation: {PlanetRotation}
            PlanetScale: {PlanetScale}
            """;
        }
    }
}