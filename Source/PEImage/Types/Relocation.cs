using System;
using System.Runtime.InteropServices;

namespace KNSoft.C4Lib.PEImage;

public class IMAGE_REL
{
    public enum I386 : UInt16
    {
        DIR32NB = 0x0007
    }

    public enum AMD64 : UInt16
    {
        ADDR32NB = 0x0003
    }

    public enum ARM64 : UInt16
    {
        ADDR32NB = 0x0002
    }

    public enum ARM : UInt16
    {
        ADDR32NB = 0x0002
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IMAGE_RELOCATION
{
    public UInt32 VirtualAddress;
    public UInt32 SymbolTableIndex;
    public UInt16 Type;
}

public class Relocation
{
    public IMAGE_RELOCATION NativeStruct;

    public Relocation(UInt32 VirtualAddress, UInt32 SymbolTableIndex, UInt16 Type)
    {
        NativeStruct = new IMAGE_RELOCATION()
        {
            VirtualAddress = VirtualAddress,
            SymbolTableIndex = SymbolTableIndex,
            Type = Type
        };
    }
}
