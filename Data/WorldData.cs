using System.Text.Json.Serialization;
using Godot;
using PlanetGame.Util;
namespace PlanetGame.Data
{
    public class WorldData
    {
        [JsonIgnore]
        public Transform3D Translation { get; private set; } = Transform3D.Identity;
        [JsonIgnore]
        public Transform3D Rotation { get; private set; } = Transform3D.Identity;
        [JsonIgnore]
        public Transform3D Scale { get; private set; } = Transform3D.Identity;

        public float Radius;
        public float HeightScale;

        public WorldData(float radius, float heightScale)
        {
            Radius = radius;
            HeightScale = heightScale;
            OrientatePlanet();
        }

        public void TranslatePlanet(Vector3 offset)
        {
            Translation = Translation.Translated(offset);
        }

        public void RotatePlanet(Vector3 axis, float angle)
        {
            Rotation = Rotation.Rotated(axis, angle).Orthonormalized();
        }

        public void ScalePlanet(Vector3 scale)
        {
            Scale = Scale.Scaled(scale);
        }

        public void OrientatePlanet()
        {
            Translation = Transform3D.Identity.Translated(Vector3.Back * -Radius);
            Scale = Transform3D.Identity.Scaled(Vector3.One * Radius);
        }

        public Transform3D GetPlanetTransform(bool translation = true, bool rotation = true, bool scale = true)
        {
            Transform3D transform = Transform3D.Identity;

            if (translation)
                transform *= Translation;

            if (rotation)
                transform *= Rotation;

            if (scale)
                transform *= Scale;

            return transform;
        }

        public Vector3 LocalToPlanet(Vector3 localPoint)
        {
            return Scale * localPoint;
        }

        public Vector3 PlanetToLocal(Vector3 planetPoint)
        {
            return Scale.AffineInverse() * planetPoint;
        }

        public Vector3 PlanetToWorld(Vector3 planetPoint)
        {
            return Translation * (Rotation * planetPoint);
        }

        public Vector3 WorldToPlanet(Vector3 worldPoint)
        {
            Vector3 translatedPoint = Translation.AffineInverse() * worldPoint;
            return Rotation.AffineInverse() * translatedPoint;
        }

        public Vector3 LocalToWorld(Vector3 localPoint)
        {
            return PlanetToWorld(LocalToPlanet(localPoint));
        }

        public Vector3 WorldToLocal(Vector3 worldPoint)
        {
            return PlanetToLocal(WorldToPlanet(worldPoint));
        }

        /// <summary>
        /// Serializes this data to match the expected GPU buffer layout.
        /// </summary>
        /// <remarks>
        /// <code>
        /// layout(std430, binding = X) readonly buffer WorldData {
        ///     float radius;
        ///     float height_scale;
        ///     vec2 padding;
        ///     mat4 planet_transform_matrix;
        /// };
        /// </code>
        /// </remarks>
        public byte[] ToBytes()
        {
            return [
                .. Utilities.ToBytesSingle(Radius),
                .. Utilities.ToBytesSingle(Radius * HeightScale),
                .. Utilities.ToBytesSingle(0),
                .. Utilities.ToBytesSingle(0),
                .. Utilities.ToBytesSingle(Utilities.ToProjection(GetPlanetTransform())),
            ];
        }
        public override string ToString()
        {
            return $"{GetType().Name} {{ Radius = {Radius}, Position = {Translation}, Rotation = {Rotation}, Scale = {Scale} }}";
        }
    }
}