using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace KNSoft.C4Lib;

static partial class Rtl
{
    public static T[] ResizeArray<T>([DisallowNull] T[] RefArray, Int32 NewSize)
    {
        T[] NewArray = RefArray;
        Array.Resize(ref NewArray, NewSize);
        return NewArray;
    }

    public static T[] CombineArray<T>(params T[][] Arrays) where T : new()
    {
        T[] NewArray = new T[Arrays.Sum(x => x.Length)];
        Int32 Offset = 0;
        Int32 ItemSize = Marshal.SizeOf(new T().GetType());
        foreach (T[] ArrayItem in Arrays)
        {
            System.Buffer.BlockCopy(ArrayItem, 0, NewArray, Offset, ArrayItem.Length * ItemSize);
            Offset += ArrayItem.Length;
        }
        return NewArray;
    }
}
