using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Linq;

namespace KNSoft.C4Lib;

static partial class Rtl
{
    public static T[] ArrayResize<T>([DisallowNull] T[] RefArray, Int32 NewSize)
    {
        T[] NewArray = RefArray;
        Array.Resize(ref NewArray, NewSize);
        return NewArray;
    }

    public static T[] ArrayCombine<T>(params T[][] Arrays) where T : new()
    {
        T[] NewArray = new T[Arrays.Sum(x => x.Length)];
        Int32 Offset = 0;
        Int32 ItemSize = Marshal.SizeOf(new T().GetType());
        foreach (T[] ArrayItem in Arrays)
        {
            Buffer.BlockCopy(ArrayItem, 0, NewArray, Offset, ArrayItem.Length * ItemSize);
            Offset += ArrayItem.Length;
        }
        return NewArray;
    }

    public static T[] ArraySlice<T>([DisallowNull] T[] RefArray, Int32 Start, Int32 Length)
    {
        T[] NewArray = new T[Length];
        Array.Copy(RefArray, Start, NewArray, 0, Length);
        return NewArray;
    }
}
