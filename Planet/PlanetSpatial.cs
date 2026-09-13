using Godot;
using PlanetGame.Data;

public partial class PlanetSpatial
{
    private static TessellationData TessellationData => SaveManager.TessellationData;
    private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
    
     #region Planet Transforms
    public Transform3D PlanetTranslation { get; private set; } = Transform3D.Identity;
    public Transform3D PlanetRotation { get; private set; } = Transform3D.Identity;
    public Transform3D PlanetScale { get; private set; } = Transform3D.Identity;

    public PlanetSpatial() {}

    public void TranslatePlanet(Vector3 offset)
    {
        PlanetTranslation = PlanetTranslation.Translated(offset);
    }

    public void RotatePlanet(Vector3 axis, float angle)
    {
        PlanetRotation = PlanetRotation.Rotated(axis, angle).Orthonormalized();
    }

    public void ScalePlanet(Vector3 scale)
    {
        PlanetScale = PlanetScale.Scaled(scale);
    }

    public void ReorientatePlanet()
    {
        PlanetTranslation = Transform3D.Identity.Translated(Vector3.Back * (-TessellationData.Radius));
        PlanetScale = Transform3D.Identity.Scaled(Vector3.One * TessellationData.Radius);
    }

    public Transform3D GetPlanetTransform(bool translation = true, bool rotation = true, bool scale = true)
    {
        Transform3D transform = Transform3D.Identity;

        if (translation)
            transform *= PlanetTranslation;

        if (rotation)
            transform *= PlanetRotation;

        if (scale)
            transform *= PlanetScale;

        return transform;
    }

    public Vector3 LocalToPlanet(Vector3 localPoint)
    {
        return PlanetScale * localPoint;
    }

    public Vector3 PlanetToLocal(Vector3 planetPoint)
    {
        return PlanetScale.AffineInverse() * planetPoint;
    }

    public Vector3 PlanetToWorld(Vector3 planetPoint)
    {
        return PlanetTranslation * (PlanetRotation * planetPoint);
    }

    public Vector3 WorldToPlanet(Vector3 worldPoint)
    {
        Vector3 translatedPoint = PlanetTranslation.AffineInverse() * worldPoint;
        return PlanetRotation.AffineInverse() * translatedPoint;
    }

    public Vector3 LocalToWorld(Vector3 localPoint)
    {
        return PlanetToWorld(LocalToPlanet(localPoint));
    }

    public Vector3 WorldToLocal(Vector3 worldPoint)
    {
        return PlanetToLocal(WorldToPlanet(worldPoint));
    }

    #endregion
}