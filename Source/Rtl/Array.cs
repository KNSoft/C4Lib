using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace KNSoft.C4Lib;

static partial class Rtl
{
    public static T[] ResizeArray<T>([DisallowNull] T[] RefArray, Int32 NewSize)
    {
        T[] NewArray = RefArray;
        Array.Resize(ref NewArray, NewSize);
        return NewArray;
    }

    public static T[] CombineArray<T>(params T[][] Arrays)
    {
        T[] NewArray = new T[Arrays.Sum(x => x.Length)];
        Int32 Offset = 0;
        foreach (T[] ArrayItem in Arrays)
        {
            System.Buffer.BlockCopy(ArrayItem, 0, NewArray, Offset, ArrayItem.Length);
            Offset += ArrayItem.Length;
        }
        return NewArray;
    }
}
