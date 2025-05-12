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

        public static Span<byte> ToBytesSingle<T>(T data) where T : unmanaged
        {
            return ToBytes(MemoryMarshal.CreateSpan(ref data, 1));
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

        public static float LimitRotation(float rotation)
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

        public static float GetPixelBilinear(Image image, float u, float v)
        {
            u = Mathf.Clamp(u, 0f, 1f);
            v = Mathf.Clamp(v, 0f, 1f);

            int width = image.GetWidth();
            int height = image.GetHeight();

            float x = u * (width - 1);
            float y = v * (height - 1);

            int x0 = Mathf.Clamp((int)x, 0, Mathf.Max(0, width - 2));
            int y0 = Mathf.Clamp((int)y, 0, Mathf.Max(0, height - 2));
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float tx = x - x0;
            float ty = y - y0;

            float v00 = image.GetPixel(x0, y0).R;
            float v10 = image.GetPixel(x1, y0).R;
            float v01 = image.GetPixel(x0, y1).R;
            float v11 = image.GetPixel(x1, y1).R;

            float a = Mathf.Lerp(v00, v10, tx);
            float b = Mathf.Lerp(v01, v11, tx);
            return Mathf.Lerp(a, b, ty);
        }


    }
}