using System;
using Godot;
using PlanetGame.Data;
using PlanetGame.Planet.Rendering;
using PlanetGame.Planet.Rendering.VirtualTexturing;

public class PlanetQuery
{
    private static TessellationData TessellationData => SaveManager.TessellationData;
    private static VirtualTextureData VirtualTextureData => SaveManager.VirtualTextureData;
    private PlanetSpatial _planetSpatial;
    private PlanetRenderer _planetRenderer;
    private CustomCamera _queryCamera;


    public PlanetQuery(CustomCamera queryCamera, PlanetSpatial planetSpatial, PlanetRenderer planetRenderer)
    {
        _queryCamera = queryCamera;
        _planetSpatial = planetSpatial;
        _planetRenderer = planetRenderer;
    }

    public struct PlanetSurfacePoint(Vector3 localSpherePoint, Vector3 localCubePoint, uint normalId, Vector2 uv, uint mipIndex)
    {
        public Vector3 LocalSpherePoint = localSpherePoint;
        public Vector3 LocalCubePoint = localCubePoint;
        public uint NormalId = normalId;
        public Vector2 UV = uv;
        public uint MipIndex = mipIndex;

        public bool Equals(PlanetSurfacePoint other)
        {
            return UV == other.UV &&
                NormalId == other.NormalId &&
                MipIndex == other.MipIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is PlanetSurfacePoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UV, NormalId, MipIndex);
        }

    }

    public bool TryGetMouseSurfacePoint(out PlanetSurfacePoint surfacePoint, bool isLocalSpace = false, uint? desiredMipIndex = null)
    {
        Vector3 planetMousePoint = GetPlanetMousePosition(true);
        return TryGetSurfacePoint(planetMousePoint, out surfacePoint, isLocalSpace, desiredMipIndex);
    }
    public bool TryGetSurfacePoint(Vector3 point, out PlanetSurfacePoint surfacePoint, bool isLocalSpace = false, uint? desiredMipIndex = null)
    {
        (Vector3 localSpherePoint, Vector3 localCubePoint) = GetLocalPointsOnPlanet(point, isLocalSpace);

        if (!localSpherePoint.IsFinite() || !localCubePoint.IsFinite())
        {
            surfacePoint = default;
            return false;
        }

        Vector3 normal = VectorUtils.IsolateNormal(localCubePoint);
        uint normalId = VectorUtils.NormalToNormalID[normal];
        Vector2 uv = VectorUtils.PointOnCubeToPlaneUV(normalId, localCubePoint);

        uint mip;
        if (desiredMipIndex != null)
        {
            mip = desiredMipIndex.Value;
        }
        else
        {
            int lod = Mathf.CeilToInt(GetLodOfPoint(_planetSpatial.LocalToPlanet(localSpherePoint), false));
            mip = VirtualTextureData.GetMipIndex(VirtualTextureData.LodToMipMap[lod]);
        }

        surfacePoint = new PlanetSurfacePoint(
            localSpherePoint,
            localCubePoint,
            normalId,
            uv,
            mip
        );

        return true;
    }

    public (Vector3 localSpherePoint, Vector3 localCubePoint) GetLocalPointsOnPlanet(Vector3 point, bool isLocalSpace)
    {
        Vector3 localPoint = isLocalSpace
            ? point
            : _planetSpatial.WorldToLocal(point);

        if (localPoint.IsZeroApprox() || !localPoint.IsFinite())
            return (Vector3.Inf, Vector3.Inf);

        Vector3 localSpherePoint = localPoint.Normalized();
        Vector3 localCubePoint = VectorUtils.PointOnSphereToPointOnCube(localSpherePoint);

        return (localSpherePoint, localCubePoint);
    }

    public Vector3 GetPlanetMousePosition(bool localPosition = false)
    {
        Vector3 position = GetPlanetScreenPosition(
            _queryCamera.GetViewport().GetMousePosition()
        );

        return localPosition ? _planetSpatial.PlanetToLocal(position) : position;
    }

    public Vector3 GetPlanetScreenPosition(Vector2 mousePosition)
    {
        Vector3 localPickedPosition = _planetRenderer.SparseVirtualTexture.GetLocalMousePosition(
            mousePosition,
            _queryCamera.GetViewport().GetVisibleRect().Size
        );

        if (!localPickedPosition.IsFinite())
            return Vector3.Inf;

        return _planetSpatial.LocalToPlanet(localPickedPosition);
    }

    public float GetLodOfPoint(Vector3 point, bool inWorldSpace)
    {
        Vector3 planetPoint = inWorldSpace ? _planetSpatial.WorldToPlanet(point) : point;
        Vector3 cameraPlanetPoint = _planetSpatial.WorldToPlanet(_queryCamera.GlobalPosition);

        float distanceToCamera = planetPoint.DistanceTo(cameraPlanetPoint);

        float numerator = (distanceToCamera - _planetRenderer.HeightOffset) * Mathf.Tan(_queryCamera.GetCameraFov(true) / 2);
        float denominator = Mathf.Sqrt2 * TessellationData.SubFactor * TessellationData.Radius;

        return Mathf.Clamp(
            -Mathf.Log(numerator / denominator) / Mathf.Log(2.0f),
            TessellationData.MinimumLod,
            TessellationData.MaximumLod
        );
    }

    public float GetHeightAtPoint(Vector3 point)
    {
        if (!TryGetSurfacePoint(point, out PlanetSurfacePoint surfacePoint))
            return float.NaN;


        uint normalId = surfacePoint.NormalId;
        uint mip = _planetRenderer.SparseVirtualTexture.SampleConsolidatedIndirectionTexture(normalId, surfacePoint.UV);
        float mipGridSize = VirtualTextureData.GetMipSize(mip);
        Vector2I tileCoords = (Vector2I)(surfacePoint.UV * mipGridSize).Floor();

        Image heightmap = _planetRenderer.SparseVirtualTexture.GetTileCache(TileCache.TileCacheType.HEIGHTMAP).GetTileImage(mip, normalId, (uint)tileCoords.X, (uint)tileCoords.Y);

        if (heightmap == null)
            return float.NaN;

        Vector2 tileMinUV = new Vector2(tileCoords.X, tileCoords.Y) / mipGridSize;

        Vector2 tileLocalUV = (surfacePoint.UV - tileMinUV) * mipGridSize;

        float elevation = Sampler.SampleBilinear(heightmap, tileLocalUV).R;

        return elevation * TessellationData.Radius * TessellationData.HeightScale;
    }

    

}