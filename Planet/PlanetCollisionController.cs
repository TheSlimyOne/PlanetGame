using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public class PlanetCollisionController(PlanetController planetController)
{
    PlanetController PlanetController = planetController;
    const uint COLLISION_RESOLUTION = 9;
    const uint COLLISION_SQUARE = 5;

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

                if (x != COLLISION_RESOLUTION - 1 && y != COLLISION_RESOLUTION - 1)
                {
                    _collisionTriangles[triangleIndex++] = currentIndex;
                    _collisionTriangles[triangleIndex++] = currentIndex + (int)COLLISION_RESOLUTION + 1;
                    _collisionTriangles[triangleIndex++] = currentIndex + (int)COLLISION_RESOLUTION;

                    _collisionTriangles[triangleIndex++] = currentIndex;
                    _collisionTriangles[triangleIndex++] = currentIndex + 1;
                    _collisionTriangles[triangleIndex++] = currentIndex + (int)COLLISION_RESOLUTION + 1;
                }
            }
        }
    }

    private Vector3[] GenerateCollisionVertices(Vector2 tileUV, float gridStep, int normalId, float radius, Vector3 tileSphereOrigin)
    {
        Vector3[] vertices = new Vector3[_baseCollisionVertices.Length];

        for (int i = 0; i < _baseCollisionVertices.Length; i++)
        {
            Vector2 percentage = _baseCollisionVertices[i];
            Vector2 vertexUV = tileUV + percentage * gridStep;

            Vector3 cubePoint = VectorUtils.PointOnPlaneToPointOnCube(vertexUV, normalId);
            Vector3 spherePoint = cubePoint.Normalized() * (radius + 5);

            vertices[i] = spherePoint - tileSphereOrigin;
        }

        return vertices;
    }

    public void CreateCollisionPlane()
    {
        (Vector3 localSpherePoint, Vector3 localCubePoint) = PlanetController.GetLocalPointsOnPlanet(PlanetController.MainCamera.GlobalPosition, false);

        if (localSpherePoint == Vector3.Inf || localCubePoint == Vector3.Inf)
            return;

        foreach (CollisionShape3D shape in CollisionBody.GetChildren().Cast<CollisionShape3D>())
        {
            CollisionBody.RemoveChild(shape);
        }

        Vector3 normal = VectorUtils.IsolateNormal(localCubePoint);
        int normalId = VectorUtils.NormalToNormalID[normal];

        int lod = PlanetController.TerrainTessellator.MaxLod;
        float radius = PlanetController.Radius;

        int gridSize = 1 << lod;
        float gridStep = 1.0f / gridSize;

        Vector2 uv = VectorUtils.PointOnCubeToUV(normalId, localCubePoint);
        Vector2 gridCoordinate = (uv * gridSize).Floor();
        Vector2 tileMinUV = gridCoordinate / gridSize;

        Queue<Vector2> tileQueue = new();
        for (int i = -(int)COLLISION_SQUARE; i <= COLLISION_SQUARE; i++)
        {
            for (int j = -(int)COLLISION_SQUARE; j <= COLLISION_SQUARE; j++)
            {
                Vector2 tileUV = tileMinUV + new Vector2(j, i) * gridStep;

                if (tileUV.X < 0 || tileUV.Y < 0 || tileUV.X >= 1 || tileUV.Y >= 1)
                    continue;

                tileQueue.Enqueue(tileUV);
            }
        }


        while (tileQueue.Count > 0)
        {
            Vector2 tileUV = tileQueue.Dequeue();

            Vector3 tileCubeOrigin = VectorUtils.PointOnPlaneToPointOnCube(tileUV, normalId);
            Vector3 tileSphereOrigin = tileCubeOrigin.Normalized() * radius;

            Vector3[] vertices = GenerateCollisionVertices(tileUV, gridStep, normalId, radius, tileSphereOrigin);
            Vector3[] faces = new Vector3[_collisionTriangles.Length];

            for (int i = 0; i < _collisionTriangles.Length; i++)
                faces[i] = vertices[_collisionTriangles[i]];

            ConcavePolygonShape3D shape = new();
            shape.SetFaces(faces);

            CollisionShape3D collisionShape = new()
            {
                Shape = shape,
                Position = tileSphereOrigin
            };


            CollisionBody.AddChild(collisionShape);

            // GD.Print("Hi");
        }
        




    }
}



