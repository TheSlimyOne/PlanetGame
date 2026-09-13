using System.Collections.Generic;
using System.Linq;
using Godot;

namespace PlanetGame.Planet.Rendering.VirtualTexturing
{
    // Not a class I plan to use, but useful to have
    public static class PlanetMeshGenerator
    {
        private enum Direction
        {
            Up,
            Down,
            Left,
            Right,
            BottomLeft,
            BottomRight,
            TopLeft,
            TopRight
        }

        public static ArrayMesh GetPlanetMesh(int resolution, Image heightmap, float strength)
        {
            const int FACE_COUNT = 6;

            Vector3[] vertices = new Vector3[FACE_COUNT * resolution * resolution];
            Vector3[] normals = new Vector3[FACE_COUNT * resolution * resolution];
            Vector2[] uvs = new Vector2[FACE_COUNT * resolution * resolution];
            int[] triangles = new int[FACE_COUNT * (resolution - 1) * (resolution - 1) * 6];

            int triangleIndex = 0;
            int vertexIndex = 0;

            for (int face = 0; face < FACE_COUNT; face++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        int currentIndex = vertexIndex++;

                        Vector2 percentage = new Vector2(x, y) / (resolution - 1);
                        Vector3 cubePoint = VectorUtils.UVToPointOnCube(face, percentage);
                        Vector3 spherePoint = VectorUtils.PointOnCubeToPointOnSphere(cubePoint);
                        Vector2 uv = VectorUtils.PointOnSphereToUV(spherePoint);

                        Color pixel = Sampler.SampleBilinear(heightmap, uv);
                        Vector3 vertex = spherePoint + spherePoint.Normalized() * pixel.R * strength;

                        vertices[currentIndex] = vertex;
                        uvs[currentIndex] = uv;
                        normals[currentIndex] = Vector3.Zero;

                        if (x == resolution - 1 || y == resolution - 1)
                            continue;

                        bool isXEven = x % 2 == 0;
                        bool isYEven = y % 2 == 0;

                        if ((isXEven && isYEven) || (!isXEven && !isYEven))
                        {
                            triangles[triangleIndex++] = currentIndex;
                            triangles[triangleIndex++] = currentIndex + resolution + 1;
                            triangles[triangleIndex++] = currentIndex + resolution;

                            triangles[triangleIndex++] = currentIndex;
                            triangles[triangleIndex++] = currentIndex + 1;
                            triangles[triangleIndex++] = currentIndex + resolution + 1;
                        }
                        else
                        {
                            triangles[triangleIndex++] = currentIndex;
                            triangles[triangleIndex++] = currentIndex + 1;
                            triangles[triangleIndex++] = currentIndex + resolution;

                            triangles[triangleIndex++] = currentIndex + 1;
                            triangles[triangleIndex++] = currentIndex + resolution + 1;
                            triangles[triangleIndex++] = currentIndex + resolution;
                        }
                    }
                }
            }

            CalculateNormals(vertices, triangles, normals, resolution);

            Godot.Collections.Array arrays = [];
            arrays.Resize((int)Mesh.ArrayType.Max);

            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Index] = triangles;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;

            ArrayMesh triangleMesh = new();
            triangleMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

            return triangleMesh;
        }

        private static void CalculateNormals(Vector3[] vertices, int[] triangles, Vector3[] normals, int resolution)
        {
            (int faceA, int faceB, Direction directionA, Direction directionB, bool isReversedA, bool isReversedB)[] adjacencies =
            [
                (0, 2, Direction.Down, Direction.Left, false, false),
                (0, 3, Direction.Up, Direction.Right, false, false),
                (0, 4, Direction.Left, Direction.Right, false, false),
                (0, 5, Direction.Right, Direction.Left, false, false),

                (1, 2, Direction.Down, Direction.Right, true, false),
                (1, 3, Direction.Up, Direction.Left, true, false),
                (1, 4, Direction.Right, Direction.Left, false, false),
                (1, 5, Direction.Left, Direction.Right, false, false),

                (2, 4, Direction.Down, Direction.Down, false, true),
                (2, 5, Direction.Up, Direction.Down, false, false),
                (3, 4, Direction.Down, Direction.Up, true, true),
                (3, 5, Direction.Up, Direction.Up, true, false)
            ];

            (int faceA, int faceB, int faceC, Direction directionA, Direction directionB, Direction directionC)[] corners =
            [
                (0, 2, 4, Direction.BottomLeft, Direction.BottomLeft, Direction.BottomRight),
                (1, 2, 4, Direction.BottomRight, Direction.BottomRight, Direction.BottomLeft),
                (1, 3, 4, Direction.TopRight, Direction.BottomLeft, Direction.TopLeft),
                (1, 3, 5, Direction.TopLeft, Direction.TopLeft, Direction.TopRight),
                (1, 2, 5, Direction.BottomLeft, Direction.TopRight, Direction.BottomRight),
                (0, 2, 5, Direction.BottomRight, Direction.TopLeft, Direction.BottomLeft),
                (0, 3, 4, Direction.TopLeft, Direction.BottomRight, Direction.TopRight),
                (0, 3, 5, Direction.TopRight, Direction.TopRight, Direction.TopLeft)
            ];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int indexA = triangles[i];
                int indexB = triangles[i + 1];
                int indexC = triangles[i + 2];

                Vector3 edge1 = vertices[indexB] - vertices[indexA];
                Vector3 edge2 = vertices[indexC] - vertices[indexA];

                Vector3 faceNormal = edge2.Cross(edge1).Normalized();

                normals[indexA] += faceNormal;
                normals[indexB] += faceNormal;
                normals[indexC] += faceNormal;
            }

            foreach ((int faceA, int faceB, Direction directionA, Direction directionB, bool isReversedA, bool isReversedB) in adjacencies)
            {
                List<int> faceAIndices = GetIndicesFromDirection(directionA, resolution, isReversedA);
                List<int> faceBIndices = GetIndicesFromDirection(directionB, resolution, isReversedB);

                for (int i = 0; i < faceAIndices.Count; i++)
                {
                    int vertexIndexA = faceA * resolution * resolution + faceAIndices[i];
                    int vertexIndexB = faceB * resolution * resolution + faceBIndices[i];

                    Vector3 newNormal = normals[vertexIndexA] + normals[vertexIndexB];

                    normals[vertexIndexA] = newNormal;
                    normals[vertexIndexB] = newNormal;
                }
            }

            foreach ((int faceA, int faceB, int faceC, Direction directionA, Direction directionB, Direction directionC) in corners)
            {
                int vertexIndexA = faceA * resolution * resolution + GetCornerIndex(directionA, resolution);
                int vertexIndexB = faceB * resolution * resolution + GetCornerIndex(directionB, resolution);
                int vertexIndexC = faceC * resolution * resolution + GetCornerIndex(directionC, resolution);

                Vector3 newNormal = normals[vertexIndexA] + normals[vertexIndexB] + normals[vertexIndexC];

                normals[vertexIndexA] = newNormal;
                normals[vertexIndexB] = newNormal;
                normals[vertexIndexC] = newNormal;
            }

            for (int i = 0; i < normals.Length; i++)
                normals[i] = normals[i].Normalized();
        }

        private static List<int> GetIndicesFromDirection(Direction direction, int resolution, bool isReversed)
        {
            IEnumerable<int> indices = direction switch
            {
                Direction.Up => Enumerable.Range(0, resolution).Select(value => resolution * (resolution - 1) + value),
                Direction.Down => Enumerable.Range(0, resolution),
                Direction.Left => Enumerable.Range(0, resolution).Select(value => value * resolution),
                Direction.Right => Enumerable.Range(0, resolution).Select(value => value * resolution + resolution - 1),
                _ => []
            };

            indices = indices.Skip(1).SkipLast(1);

            return [.. isReversed ? indices.Reverse() : indices];
        }

        private static int GetCornerIndex(Direction direction, int resolution)
        {
            return direction switch
            {
                Direction.BottomLeft => 0,
                Direction.BottomRight => resolution - 1,
                Direction.TopLeft => resolution * (resolution - 1),
                Direction.TopRight => resolution * resolution - 1,
                _ => -1
            };
        }
    }
}
