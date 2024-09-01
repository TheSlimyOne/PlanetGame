using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Godot;


public static class Utilities
{
   // Based discord user created these functions: idrmzit
    public static Span<byte> ToBytes<T>(Span<T> data) where T : unmanaged
    {
        return MemoryMarshal.Cast<T, byte>(data);
    }

    public static Span<byte> ToBytesSingle<T>(T data) where T : unmanaged
    {   
        return ToBytes(MemoryMarshal.CreateSpan(ref data, 1));
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

    public static string ToBinary(int number, bool isPadded=true)
    {
        return isPadded ? Convert.ToString(number, 2).PadZeros(32) : Convert.ToString(number, 2);
    }

    public static string ToBinary(uint number, bool isPadded=true)
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

}