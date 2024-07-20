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

}