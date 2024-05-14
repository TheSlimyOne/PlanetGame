using System;
using Godot;

public partial class QuadTreeMeshes
{

    private MultiMesh[] _meshes = new MultiMesh[16];
    public MultiMesh[] Meshes
    {
        get => _meshes;
    }

    public readonly int _resolution;

    public QuadTreeMeshes(int resolution)
    {
        _resolution = resolution;
    }

    public void Initialize()
    {
        for (int i = 0; i < 16; i++)
        {
            Vector3[] vertices = new Vector3[_resolution * _resolution];
            Vector2[] uvs = new Vector2[_resolution * _resolution];
            int[] triangles = new int[(_resolution - 1) * (_resolution - 1) * 6];

            bool isRightFan = i % 2 == 1;
            bool isLeftFan = i % 4 == 2 || i % 4 == 3;
            bool isTopFan = (i >= 4 && i < 8) || i >= 12;
            bool isBottomFan = i >= 8;


            int triIndex = 0;
            for (int x = 0; x < _resolution; x++)
            {
                for (int y = 0; y < _resolution; y++)
                {
                    int vertexIndex = x + y * _resolution;

                    Vector2 percentage = new Vector2(x, y) / (_resolution - 1);

                    vertices[vertexIndex] = Vector3.Zero;
                    uvs[vertexIndex] = percentage;

                    if (x > 0 && x < _resolution - 2 && y > 0 && y < _resolution - 2)
                    {
                        triangles[triIndex++] = vertexIndex;
                        triangles[triIndex++] = vertexIndex + _resolution;
                        triangles[triIndex++] = (x % 2 == 1 && y % 2 == 1) || (x % 2 == 0 & y % 2 == 0) ? vertexIndex + _resolution + 1 : vertexIndex + 1;
                        triangles[triIndex++] = (x % 2 == 1 && y % 2 == 1) || (x % 2 == 0 & y % 2 == 0) ? vertexIndex :  vertexIndex + _resolution;
                        triangles[triIndex++] = vertexIndex + _resolution + 1;
                        triangles[triIndex++] = vertexIndex + 1;
                    }

                    if (y == 0 && x >= 1 && x < _resolution - 1)
                    {
                        if (isBottomFan)
                        {
                            if (x % 2 == 1)
                            {
                                triangles[triIndex++] = vertexIndex - 1;
                                triangles[triIndex++] = vertexIndex + _resolution;
                                triangles[triIndex++] = vertexIndex + 1;
                            }
                            else
                            {
                                triangles[triIndex++] = vertexIndex;
                                triangles[triIndex++] = vertexIndex + _resolution - 1;
                                triangles[triIndex++] = vertexIndex + _resolution;
                                triangles[triIndex++] = vertexIndex;
                                triangles[triIndex++] = vertexIndex + _resolution;
                                triangles[triIndex++] = vertexIndex + _resolution + 1;
                            }
                        }
                        else
                        {
                            triangles[triIndex++] = vertexIndex;
                            triangles[triIndex++] = x % 2 == 1 ? vertexIndex - 1 : vertexIndex + _resolution - 1;
                            triangles[triIndex++] = vertexIndex + _resolution;
                            triangles[triIndex++] = vertexIndex + _resolution;
                            triangles[triIndex++] = x % 2 == 1 ? vertexIndex + 1 : vertexIndex + _resolution + 1;
                            triangles[triIndex++] = vertexIndex;
                        }
                    }

                    if (y == _resolution - 1 && x >= 1 && x < _resolution - 1)
                    {
                        if (isTopFan)
                        {
                            if (x % 2 == 1)
                            {
                                triangles[triIndex++] = vertexIndex - 1;
                                triangles[triIndex++] = vertexIndex + 1;
                                triangles[triIndex++] = vertexIndex - _resolution;
                            }
                            else
                            {
                                triangles[triIndex++] = vertexIndex;
                                triangles[triIndex++] = vertexIndex - _resolution;
                                triangles[triIndex++] = vertexIndex - _resolution - 1;
                                triangles[triIndex++] = vertexIndex;
                                triangles[triIndex++] = vertexIndex - _resolution + 1;
                                triangles[triIndex++] = vertexIndex - _resolution;
                            }
                        }
                        else
                        {
                            triangles[triIndex++] = vertexIndex;
                            triangles[triIndex++] = vertexIndex - _resolution;
                            triangles[triIndex++] = x % 2 == 1 ? vertexIndex - 1 : vertexIndex - _resolution - 1;
                            triangles[triIndex++] = x % 2 == 1 ? vertexIndex + 1 : vertexIndex - _resolution + 1;
                            triangles[triIndex++] = vertexIndex - _resolution;
                            triangles[triIndex++] = vertexIndex;
                        }
                    }

                    if (x == 0 && y >= 1 && y < _resolution - 1)
                    {
                        if (isLeftFan)
                        {
                            if (y % 2 == 1)
                            {
                                triangles[triIndex++] = vertexIndex + _resolution;
                                triangles[triIndex++] = vertexIndex + 1;
                                triangles[triIndex++] = vertexIndex - _resolution;
                            }
                            else
                            {
                                triangles[triIndex++] = vertexIndex;
                                triangles[triIndex++] = vertexIndex + 1;
                                triangles[triIndex++] = vertexIndex - _resolution + 1;
                                triangles[triIndex++] = vertexIndex;
                                triangles[triIndex++] = vertexIndex + _resolution + 1;
                                triangles[triIndex++] = vertexIndex + 1;
                            }

                        }
                        else
                        {
                            triangles[triIndex++] = vertexIndex;
                            triangles[triIndex++] = y % 2 == 1 ? vertexIndex + _resolution:vertexIndex + _resolution + 1;
                            triangles[triIndex++] = vertexIndex + 1;
                            triangles[triIndex++] = vertexIndex + 1;
                            triangles[triIndex++] = y % 2 == 1 ? vertexIndex - _resolution : vertexIndex -_resolution + 1;
                            triangles[triIndex++] = vertexIndex;                         
                        }
                    }

                    if (x == _resolution - 1 && y >= 1 && y < _resolution - 1)
                    {
                        if (isRightFan)
                        {
                            if (y % 2 == 1)
                            {
                                triangles[triIndex++] = vertexIndex - _resolution;
                                triangles[triIndex++] = vertexIndex - 1;
                                triangles[triIndex++] = vertexIndex + _resolution;
                            }
                            else
                            {
                                triangles[triIndex++] = vertexIndex;
                                triangles[triIndex++] = vertexIndex - _resolution - 1;
                                triangles[triIndex++] = vertexIndex - 1;
                                triangles[triIndex++] = vertexIndex;
                                triangles[triIndex++] = vertexIndex - 1;
                                triangles[triIndex++] = vertexIndex + _resolution - 1;
                            }
                        }
                        else
                        {
                            triangles[triIndex++] = vertexIndex;
                            triangles[triIndex++] = y % 2 == 1 ? vertexIndex - _resolution : vertexIndex - _resolution - 1;
                            triangles[triIndex++] = vertexIndex - 1;
                            triangles[triIndex++] = vertexIndex - 1;
                            triangles[triIndex++] = y % 2 == 1 ? vertexIndex + _resolution : vertexIndex + _resolution - 1;
                            triangles[triIndex++] = vertexIndex; 
                        }
                    }
                }
            }

            ArrayMesh mesh = new ArrayMesh();
            Godot.Collections.Array arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Index] = triangles;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);


            _meshes[i] = new MultiMesh()
            {
                Mesh = mesh,
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                InstanceCount = 0,
                UseCustomData = true
            };

        }

    }



    public MultiMeshInstance3D[] SpawnDemoMeshes()
    {
        MultiMeshInstance3D[] _multiMeshInstances = new MultiMeshInstance3D[16];

        for (int i = 0; i < 16; i++)
        {
            _multiMeshInstances[i] = new MultiMeshInstance3D()
            {
                Multimesh = _meshes[i],

            };

            _multiMeshInstances[i].Multimesh.InstanceCount = 1;
            _multiMeshInstances[i].ExtraCullMargin = 2 * 50;
            Transform3D transform = new Transform3D(Basis.Identity, new Vector3(i * 2, 0, 0));
            _multiMeshInstances[i].Multimesh.SetInstanceTransform(0, transform);

        }

        return _multiMeshInstances;
    }
}