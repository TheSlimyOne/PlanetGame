using System;
using Godot;

public class PlanetCollisionController(PlanetController planetController)
{
    PlanetController PlanetController = planetController;
    const int COLLISION_RESOLUTION = 9;
    public void CreateCollisionPlane()
    {
        (Vector3 localSpherePoint, Vector3 localCubePoint) = PlanetController.GetLocalPointsOnPlanet(PlanetController.MainCamera.GlobalPosition, false);

        if (localSpherePoint == Vector3.Inf || localCubePoint == Vector3.Inf)
            return;

        Vector3 normal = VectorUtils.IsolateNormal(localCubePoint);
        int normalId = VectorUtils.NormalToNormalID[normal];

        int lod = PlanetController.TerrainTessellator.MaxLod;
        float radius = PlanetController.Radius;

        int gridSize = 1 << lod;
        float gridStep = 1.0f / gridSize;

        Vector2 uv = VectorUtils.PointOnCubeToUV(normalId, localCubePoint);
        Vector2 gridCoordinate = (uv * gridSize).Floor();
        Vector2 tileMinUV = gridCoordinate / gridSize;

        Vector3 tileCubeOrigin = VectorUtils.PointOnPlaneToPointOnCube(tileMinUV, normalId);
        Vector3 tileSphereOrigin = tileCubeOrigin.Normalized() * radius;

        Vector3[] vertices = new Vector3[COLLISION_RESOLUTION * COLLISION_RESOLUTION];
        Vector2[] uvs = new Vector2[COLLISION_RESOLUTION * COLLISION_RESOLUTION];
        int[] triangles = new int[(COLLISION_RESOLUTION - 1) * (COLLISION_RESOLUTION - 1) * 6];

        int vertexIndex = 0;
        int triIndex = 0;

        for (int y = 0; y < COLLISION_RESOLUTION; y++)
        {
            for (int x = 0; x < COLLISION_RESOLUTION; x++)
            {
                int currentIndex = vertexIndex++;

                Vector2 percentage = new Vector2(x, y) / (COLLISION_RESOLUTION - 1);
                Vector2 vertexUV = tileMinUV + percentage * gridStep;

                Vector3 cubePoint = VectorUtils.PointOnPlaneToPointOnCube(vertexUV, normalId);
                Vector3 spherePoint = cubePoint.Normalized() * radius;

                vertices[currentIndex] = spherePoint - tileSphereOrigin;
                uvs[currentIndex] = percentage;

                if (x != COLLISION_RESOLUTION - 1 && y != COLLISION_RESOLUTION - 1)
                {
                    triangles[triIndex++] = currentIndex;
                    triangles[triIndex++] = currentIndex + COLLISION_RESOLUTION + 1;
                    triangles[triIndex++] = currentIndex + COLLISION_RESOLUTION;

                    triangles[triIndex++] = currentIndex;
                    triangles[triIndex++] = currentIndex + 1;
                    triangles[triIndex++] = currentIndex + COLLISION_RESOLUTION + 1;
                }
            }
        }

        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);

        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Index] = triangles;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;

        ArrayMesh collisionMesh = new();
        collisionMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        MeshInstance3D gridMesh = new()
        {
            Mesh = collisionMesh,
            Position = tileSphereOrigin,
            MaterialOverride = new ShaderMaterial()
            {
                Shader = new Shader()
                {
                    Code = """
                shader_type spatial;
                render_mode unshaded;

                void fragment() {
                    ALBEDO = vec3(UV, 0.0);
                }
                """
                }
            }
        };

        PlanetController.SurfaceAttachment.AddChild(gridMesh);
    }
}



