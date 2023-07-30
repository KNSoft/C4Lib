using System;
using System.Runtime.InteropServices;

namespace KNSoft.C4Lib.PEImage;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IMAGE_IMPORT_DESCRIPTOR
{
    public UInt32 OriginalFirstThunk;
    public UInt32 TimeDateStamp;
    public UInt32 ForwarderChain;
    public UInt32 Name;
    public UInt32 FirstThunk;
}
