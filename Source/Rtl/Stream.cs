using System;
using System.IO;

namespace KNSoft.C4Lib;

static partial class Rtl
{
    public static void StreamWrite(Stream Output, Byte[] Buffer)
    {
        Output.Write(Buffer, 0, Buffer.Length);
    }
}
