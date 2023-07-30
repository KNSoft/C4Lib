using System;
using System.IO;

namespace KNSoft.C4Lib;

static partial class Rtl
{
    public static void WriteToStream(Stream Output, Byte[] Buffer)
    {
        Output.Write(Buffer, 0, Buffer.Length);
    }

    public static Byte[] GetStreamBytes(Stream Output, Int32 Offset, Int64 Length)
    {
        /* TODO: Length > Int32.MaxValue */

        Byte[] Bytes = new Byte[(Int32)Length];

        Output.Read(Bytes, Offset, (Int32)Length);

        return Bytes;
    }
}
