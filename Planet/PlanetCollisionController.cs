using System.Collections.Generic;
using System.Linq;
using Godot;
using PlanetGame.Rendering.Surface;
using PlanetGame.Rendering.VirtualTexturing;

public class PlanetCollisionController(PlanetController planetController)
{
    private readonly PlanetController _planetController = planetController;

    private static TessellationData TessellationData => SaveManager.CurrentWorldSave.TessellationData;
    private static VirtualTextureData VirtualTextureData => SaveManager.CurrentWorldSave.VirtualTextureData;

    private const uint COLLISION_RESOLUTION = 10;
    private const uint COLLISION_SQUARE = 12;
    private const float COLLISION_EDGE_HEIGHT = 50.0f;

    private Vector2[] _baseCollisionVertices;
    private int[] _collisionTriangles;

    public StaticBody3D CollisionBody = new();

    public void GenerateBaseCollisionMesh()
    {
        _baseCollisionVertices = new Vector2[COLLISION_RESOLUTION * COLLISION_RESOLUTION];
        _collisionTriangles = new int[(COLLISION_RESOLUTION - 1) * (COLLISION_RESOLUTION - 1) * 6];

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int y = 0; y < COLLISION_RESOLUTION; y++)
        {
            for (int x = 0; x < COLLISION_RESOLUTION; x++)
            {
                int currentIndex = vertexIndex++;

                Vector2 percentage = new Vector2(x, y) / (COLLISION_RESOLUTION - 1);
                _baseCollisionVertices[currentIndex] = percentage;

                if (x == COLLISION_RESOLUTION - 1 || y == COLLISION_RESOLUTION - 1)
                    continue;

                _collisionTriangles[triangleIndex++] = currentIndex;
                _collisionTriangles[triangleIndex++] = currentIndex + (int)COLLISION_RESOLUTION + 1;
                _collisionTriangles[triangleIndex++] = currentIndex + (int)COLLISION_RESOLUTION;

                _collisionTriangles[triangleIndex++] = currentIndex;
                _collisionTriangles[triangleIndex++] = currentIndex + 1;
                _collisionTriangles[triangleIndex++] = currentIndex + (int)COLLISION_RESOLUTION + 1;
            }
        }
    }

    public void CreateCollisionPlane(Vector3 from)
    {
        if (!_planetController.TryGetSurfacePoint(from, out PlanetController.PlanetSurfacePoint surfacePoint))
            return;

        ClearCollisionPlane();

        float radius = TessellationData.Radius;
        float heightScale = TessellationData.HeightScale;

        int lod = Mathf.FloorToInt(_planetController.GetLodOfPoint(surfacePoint.LocalSpherePoint, true));
        int mip = VirtualTextureData.LodToMipMap[lod];

        int gridSize = 1 << lod;
        float gridStep = 1.0f / gridSize;

        Vector2 gridCoordinate = (surfacePoint.UV * gridSize).Floor();
        Vector2 tileMinUV = gridCoordinate / gridSize;

        Queue<Vector2> tileQueue = GetCollisionTileQueue(tileMinUV, gridStep);

        float mipGridSize = VirtualTextureData.GetMipSize((uint)mip);

        float collisionMinX = Mathf.Max(0.0f, tileMinUV.X - COLLISION_SQUARE * gridStep);
        float collisionMaxX = Mathf.Min(1.0f - gridStep, tileMinUV.X + COLLISION_SQUARE * gridStep);
        float collisionMinY = Mathf.Max(0.0f, tileMinUV.Y - COLLISION_SQUARE * gridStep);
        float collisionMaxY = Mathf.Min(1.0f - gridStep, tileMinUV.Y + COLLISION_SQUARE * gridStep);

        while (tileQueue.Count > 0)
        {
            Vector2 tileUV = tileQueue.Dequeue();
            Vector2I tileCoords = (Vector2I)(tileUV * mipGridSize).Floor();

            string path = $"{mip}_{surfacePoint.NormalId}_{tileCoords.X}_{tileCoords.Y}";

            Image heightmap = SaveManager.GetTile(
                SaveManager.CurrentSave,
                SaveManager.SaveDataIdentifier.TILE_HEIGHT_MAP,
                path
            );

            if (heightmap == null)
                continue;

            bool raiseLeftEdge = Mathf.IsEqualApprox(tileUV.X, collisionMinX);
            bool raiseRightEdge = Mathf.IsEqualApprox(tileUV.X, collisionMaxX);
            bool raiseTopEdge = Mathf.IsEqualApprox(tileUV.Y, collisionMinY);
            bool raiseBottomEdge = Mathf.IsEqualApprox(tileUV.Y, collisionMaxY);

            Vector3[] vertices = GenerateCollisionVertices(
                tileUV,
                gridStep,
                surfacePoint.NormalId,
                radius,
                heightmap,
                heightScale,
                tileCoords,
                mipGridSize,
                raiseLeftEdge,
                raiseRightEdge,
                raiseTopEdge,
                raiseBottomEdge
            );

            AddCollisionShape(vertices);
        }
    }

    private void ClearCollisionPlane()
    {
        foreach (CollisionShape3D shape in CollisionBody.GetChildren().Cast<CollisionShape3D>())
            shape.QueueFree();
    }

    private Queue<Vector2> GetCollisionTileQueue(Vector2 tileMinUV, float gridStep)
    {
        Queue<Vector2> tileQueue = new();

        for (int y = -(int)COLLISION_SQUARE; y <= COLLISION_SQUARE; y++)
        {
            for (int x = -(int)COLLISION_SQUARE; x <= COLLISION_SQUARE; x++)
            {
                Vector2 tileUV = tileMinUV + new Vector2(x, y) * gridStep;

                if (tileUV.X < 0 || tileUV.Y < 0 || tileUV.X >= 1 || tileUV.Y >= 1)
                    continue;

                tileQueue.Enqueue(tileUV);
            }
        }

        return tileQueue;
    }

    private void AddCollisionShape(Vector3[] vertices)
    {
        Vector3[] faces = new Vector3[_collisionTriangles.Length];

        for (int i = 0; i < _collisionTriangles.Length; i++)
            faces[i] = vertices[_collisionTriangles[i]];

        ConcavePolygonShape3D shape = new();
        shape.SetFaces(faces);

        CollisionShape3D collisionShape = new()
        {
            Shape = shape
        };

        CollisionBody.AddChild(collisionShape);
    }

    private Vector3[] GenerateCollisionVertices(Vector2 tileUV, float gridStep, int normalId, float radius, Image heightmap, float heightScale, Vector2I tileCoords, float mipGridSize, bool raiseLeftEdge, bool raiseRightEdge, bool raiseTopEdge, bool raiseBottomEdge)
    {
        Vector3[] vertices = new Vector3[_baseCollisionVertices.Length];
        Vector2 heightmapTileMinUV = new Vector2(tileCoords.X, tileCoords.Y) / mipGridSize;

        for (int i = 0; i < _baseCollisionVertices.Length; i++)
        {
            Vector2 percentage = _baseCollisionVertices[i];

            Vector2 globalUV = tileUV + percentage * gridStep;
            Vector2 heightmapLocalUV = (globalUV - heightmapTileMinUV) * mipGridSize;

            float elevation = Sampler.SampleBilinear(heightmap, heightmapLocalUV).R;

            Vector3 cubePoint = VectorUtils.PointOnPlaneToPointOnCube(globalUV, normalId);
            Vector3 sphereNormal = VectorUtils.PointOnCubeToPointOnSphere(cubePoint);

            bool isRaisedEdge =
                (raiseLeftEdge && percentage.X == 0.0f) ||
                (raiseRightEdge && percentage.X == 1.0f) ||
                (raiseTopEdge && percentage.Y == 0.0f) ||
                (raiseBottomEdge && percentage.Y == 1.0f);

            float edgeHeight = isRaisedEdge ? COLLISION_EDGE_HEIGHT : 0.0f;

            vertices[i] = sphereNormal * radius + sphereNormal * (elevation * radius * heightScale + edgeHeight);
        }

        return vertices;
    }
}