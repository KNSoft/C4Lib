using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

[Flags]
public enum IMAGE_SCN : UInt32
{
    CNT_CODE = 0x00000020,
    CNT_INITIALIZED_DATA = 0x00000040,
    ALIGN_2BYTES = 0x00200000,
    ALIGN_4BYTES = 0x00300000,
    MEM_DISCARDABLE = 0x02000000,
    MEM_EXECUTE = 0x20000000,
    MEM_READ = 0x40000000,
    MEM_WRITE = 0x80000000
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IMAGE_SECTION_HEADER
{
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 8)]
    public Byte[] Name;
    public UInt32 VirtualSize;
    public UInt32 VirtualAddress;
    public UInt32 SizeOfRawData;
    public UInt32 PointerToRawData;
    public UInt32 PointerToRelocations;
    public UInt32 PointerToLinenumbers;
    public UInt16 NumberOfRelocations;
    public UInt16 NumberOfLinenumbers;
    public UInt32 Characteristics;
}

public class SectionHeader
{
    public IMAGE_SECTION_HEADER NativeStruct;

    public enum SCN : UInt32
    {
        data = IMAGE_SCN.CNT_INITIALIZED_DATA | IMAGE_SCN.MEM_READ | IMAGE_SCN.MEM_WRITE,
        idata = IMAGE_SCN.CNT_INITIALIZED_DATA | IMAGE_SCN.MEM_READ | IMAGE_SCN.MEM_WRITE,
        edata = IMAGE_SCN.CNT_INITIALIZED_DATA | IMAGE_SCN.MEM_READ,
        rdata = IMAGE_SCN.CNT_INITIALIZED_DATA | IMAGE_SCN.MEM_READ,
        reloc = IMAGE_SCN.CNT_INITIALIZED_DATA | IMAGE_SCN.MEM_READ | IMAGE_SCN.MEM_DISCARDABLE,
        rsrc = IMAGE_SCN.CNT_INITIALIZED_DATA | IMAGE_SCN.MEM_READ,
        text = IMAGE_SCN.CNT_CODE | IMAGE_SCN.MEM_EXECUTE | IMAGE_SCN.MEM_READ,
        tls = IMAGE_SCN.CNT_INITIALIZED_DATA | IMAGE_SCN.MEM_READ | IMAGE_SCN.MEM_WRITE
    }

    public SectionHeader(String Name, UInt32 DataOffset, UInt32 DataSize, UInt32 RelocationsOffset, UInt16 NumberOfRelocations, IMAGE_SCN Characteristics)
    {
        Byte[] NameBytes = new Byte[8];
        Encoding.UTF8.GetBytes(Name).CopyTo(NameBytes, 0);

        NativeStruct = new IMAGE_SECTION_HEADER()
        {
            Name = NameBytes,
            VirtualSize = 0,
            VirtualAddress = 0,
            PointerToLinenumbers = 0,
            NumberOfLinenumbers = 0,
            PointerToRawData = DataOffset,
            SizeOfRawData = DataSize,
            PointerToRelocations = RelocationsOffset,
            NumberOfRelocations = NumberOfRelocations,
            Characteristics = (UInt32)Characteristics
        };
    }
}
