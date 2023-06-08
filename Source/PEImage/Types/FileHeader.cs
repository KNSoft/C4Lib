using System;
using System.Runtime.InteropServices;

namespace KNSoft.C4Lib.PEImage;

public enum IMAGE_FILE_MACHINE : UInt16
{
    UNKNOWN = 0x0,
    I386 = 0x14C,
    AMD64 = 0x8664,
    ARM64 = 0xAA64,
    ARM = 0x1C0
}

[Flags]
public enum IMAGE_FILE_CHARACTERISTICS : UInt16
{
    _32BIT_MACHINE = 0x100,
    DEBUG_STRIPPED = 0x200
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IMAGE_FILE_HEADER
{
    public UInt16 Machine;
    public UInt16 NumberOfSections;
    public UInt32 TimeDateStamp;
    public UInt32 PointerToSymbolTable;
    public UInt32 NumberOfSymbols;
    public UInt16 SizeOfOptionalHeader;
    public UInt16 Characteristics;
}

public class FileHeader
{
    public Byte[] Bytes;

    public FileHeader(IMAGE_FILE_MACHINE Machine, UInt16 NumberOfSections, UInt32 PointerToSymbolTable, UInt32 NumberOfSymbols, IMAGE_FILE_CHARACTERISTICS Characteristics)
    {
        Bytes = Rtl.StructToRaw(new IMAGE_FILE_HEADER()
        {
            Machine = (UInt16)Machine,
            NumberOfSections = NumberOfSections,
            TimeDateStamp = UInt32.MaxValue,
            PointerToSymbolTable = PointerToSymbolTable,
            NumberOfSymbols = NumberOfSymbols,
            SizeOfOptionalHeader = 0,
            Characteristics = (UInt16)Characteristics
        });
    }

    public static UInt32 GetMachineBits(IMAGE_FILE_MACHINE Machine)
    {
        return Machine switch
        {
            IMAGE_FILE_MACHINE.I386 => 32,
            IMAGE_FILE_MACHINE.AMD64 => 64,
            IMAGE_FILE_MACHINE.ARM64 => 64,
            IMAGE_FILE_MACHINE.ARM => 32,
            _ => 0
        };
    }

    public static IMAGE_FILE_MACHINE GetMachineType(String Name)
    {
        return Name switch
        {
            "x86" => IMAGE_FILE_MACHINE.I386,
            "i386" => IMAGE_FILE_MACHINE.I386,

            "x64" => IMAGE_FILE_MACHINE.AMD64,
            "AMD64" => IMAGE_FILE_MACHINE.AMD64,
            "x86_64" => IMAGE_FILE_MACHINE.AMD64,

            "ARM64" => IMAGE_FILE_MACHINE.ARM64,

            "ARM" => IMAGE_FILE_MACHINE.ARM,
            "ARM32" => IMAGE_FILE_MACHINE.ARM,

            _ => IMAGE_FILE_MACHINE.UNKNOWN
        };
    }
}

