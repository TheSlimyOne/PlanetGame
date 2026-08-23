using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Godot;

namespace PlanetGame.Util
{

    public static class Utilities
    {
        // Based discord user created these functions: idrmzit
        public static Span<byte> ToBytes<T>(Span<T> data) where T : unmanaged
        {
            return MemoryMarshal.Cast<T, byte>(data);
        }

        public static Span<byte> ToBytesSingle<T>(T data) where T : unmanaged
        {
            return ToBytes<T>(new T[] { data });
        }

        public static uint ToBitFlags(ReadOnlySpan<bool> values)
        {
            uint flags = 0;

            for (int index = 0; index < values.Length && index < 32; index++)
            {
                if (values[index])
                    flags |= 1u << index;
            }

            return flags;
        }

        public static (float[,] data, float max, float min) To2Darray(Image image, bool normalized = false)
        {
            int width = image.GetWidth();
            int height = image.GetHeight();
            float[,] array = new float[width, height];

            float max = float.MinValue;
            float min = float.MaxValue;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Assuming grayscale: use the red channel (R)
                    float val = image.GetPixel(x, y).R;
                    array[x, y] = val;

                    if (val < min) min = val;
                    if (val > max) max = val;
                }
            }

            if (normalized && max != min)
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        array[x, y] = (array[x, y] - min) / (max - min);

            return (array, max, min);
        }

        public static byte[] ToBytes8(float[,] array)
        {
            int col = array.GetLength(0);
            int row = array.GetLength(1);
            byte[] result = new byte[col * row];

            for (int x = 0; x < col; x++)
            {
                for (int y = 0; y < row; y++)
                {
                    float value = array[x, y];
                    value = Math.Clamp(value, 0f, 1f);
                    byte gray = (byte)(value * 255f);
                    result[y * col + x] = gray;
                }
            }

            return result;
        }

        public static uint SizeOf<T>()
        {
            return (uint)Unsafe.SizeOf<T>();
        }

        public static Span<T> FromBytes<T>(Span<byte> data) where T : unmanaged
        {
            int length = data.Length - (data.Length % Unsafe.SizeOf<T>());
            return MemoryMarshal.Cast<byte, T>(data[..length]);
        }

        public static T FromBytesSingle<T>(Span<byte> data) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(data)[0];
        }

        public static string ToBinary(int number, bool isPadded = true)
        {
            return isPadded ? Convert.ToString(number, 2).PadZeros(32) : Convert.ToString(number, 2);
        }

        public static string ToBinary(uint number, bool isPadded = true)
        {
            return isPadded ? Convert.ToString(number, 2).PadZeros(32) : Convert.ToString(number, 2);
        }

        public static Projection ToProjection(Transform3D transformation)
        {
            return new Projection(
                new Vector4(transformation[0].X, transformation[1].X, transformation[2].X, transformation[3].X),
                new Vector4(transformation[0].Y, transformation[1].Y, transformation[2].Y, transformation[3].Y),
                new Vector4(transformation[0].Z, transformation[1].Z, transformation[2].Z, transformation[3].Z),
                new Vector4(0, 0, 0, 1)
            );
        }

        public static byte[] ToViewPushConstants(Projection viewProjectionMatrix, Vector3 cameraPosition, float fovy)
        {
            byte[] data =
            [
                .. ToBytesSingle(viewProjectionMatrix),
                .. ToBytesSingle(VectorUtils.ToVector4(cameraPosition, fovy)),
            ];

            return data;
        }

        public static MeshInstance3D DrawLineDebug(Node node, Vector3 from, Vector3 to, Color color)
        {
            ImmediateMesh lineMesh = new();

            lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, new OrmMaterial3D() { AlbedoColor = color, MetallicSpecular = 0 });
            lineMesh.SurfaceAddVertex(from);
            lineMesh.SurfaceAddVertex(to);
            lineMesh.SurfaceEnd();

            MeshInstance3D meshInstance = new()
            {
                Name = $"{from}_{to}_DEBUG",
                Mesh = lineMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            node.GetWindow().CallDeferred("add_child", meshInstance);

            return meshInstance;
        }

        public static float NormalizeAngleDegrees(float rotation)
        {
            rotation %= 360f;
            if (rotation < 0) rotation += 360f;
            return rotation;
        }

        public static string ToSnakeCase(string camelCase)
        {
            string result = Regex.Replace(camelCase, "([a-z])([A-Z])", "$1_$2");
            result = Regex.Replace(result, "([A-Z])([A-Z][a-z])", "$1_$2");
            result = result.ToLower();
            return result.Trim('_');
        }

        public static string ToCamelCase(string snakeCase)
        {
            string[] words = snakeCase.Split('_');
            for (int i = 0; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
                }
            }

            string camelCase = string.Concat(words);
            // Ensure the first character is lowercase and starts with an underscore
            return "_" + char.ToLower(camelCase[0]) + camelCase[1..];
        }

        public static Godot.Collections.Dictionary RaycastFromMouse(Camera3D camera, float rayLength)
        {
            Vector2 mousePosition = camera.GetViewport().GetMousePosition();

            Vector3 rayOrigin = camera.ProjectRayOrigin(mousePosition);
            Vector3 rayDirection = camera.ProjectRayNormal(mousePosition);
            Vector3 rayEnd = rayOrigin + rayDirection * rayLength;

            PhysicsDirectSpaceState3D spaceState = camera.GetWorld3D().DirectSpaceState;
            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);

            return spaceState.IntersectRay(query);
        }

        public static RigidBody3D SpawnTestSphere(Vector3 position, float radius)
        {
            RigidBody3D body = new()
            {
                Position = position,
                GravityScale = 0.0f,
                Mass = 1.0f,
                ContinuousCd = true,
            };

            SphereShape3D shape = new()
            {
                Radius = radius
            };

            body.AddChild(new CollisionShape3D
            {
                Shape = shape
            });

            body.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh
                {
                    Radius = radius,
                    Height = radius * 2
                }
            });

            return body;
        }

    }
}