using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.IO;

namespace KNSoft.C4Lib;

static partial class Rtl
{
    public static T RawToStruct<T>(Byte[] Bytes) where T : new()
    {
        Type anyType = new T().GetType();
        int RawSize = Marshal.SizeOf(anyType);
        if (RawSize != Bytes.Length)
        {
            throw new InvalidDataException();
        }

        IntPtr buffer = Marshal.AllocHGlobal(RawSize);
        Marshal.Copy(Bytes, 0, buffer, RawSize);
        T obj = (T)Marshal.PtrToStructure(buffer, anyType);
        Marshal.FreeHGlobal(buffer);
        return obj;
    }

    public static Byte[] StructToRaw<T>([DisallowNull] T Struct)
    {
        Byte[] Buffer = new Byte[Marshal.SizeOf(Struct)];
        IntPtr Ptr = Marshal.AllocHGlobal(Buffer.Length);
        Marshal.StructureToPtr(Struct, Ptr, true);
        Marshal.Copy(Ptr, Buffer, 0, Buffer.Length);
        Marshal.FreeHGlobal(Ptr);
        return Buffer;
    }
}
