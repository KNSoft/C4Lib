using System;
using System.Runtime.InteropServices;

namespace KNSoft.C4Lib.PEImage;

public enum IMAGE_FILE_MACHINE : UInt16
{
    UNKNOWN = 0x0,
    I386 = 0x14C,
    AMD64 = 0x8664,
    ARM64 = 0xAA64,
    ARM = 0x1C0,
    ARMNT = 0x1C4
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

            "ARM" => IMAGE_FILE_MACHINE.ARMNT,
            "ARM32" => IMAGE_FILE_MACHINE.ARMNT,
            "ARMv7" => IMAGE_FILE_MACHINE.ARMNT,

            _ => IMAGE_FILE_MACHINE.UNKNOWN
        };
    }

    public static Byte GetMachineBits(IMAGE_FILE_MACHINE Machine)
    {
        return Machine switch
        {
            IMAGE_FILE_MACHINE.I386 => 32,
            IMAGE_FILE_MACHINE.AMD64 => 64,
            IMAGE_FILE_MACHINE.ARM64 => 64,
            IMAGE_FILE_MACHINE.ARM => 32,
            IMAGE_FILE_MACHINE.ARMNT => 32,
            _ => 0
        };
    }

    public static Byte GetSizeOfPointer(IMAGE_FILE_MACHINE Machine)
    {
        return (Byte)(GetMachineBits(Machine) / 8);
    }

    public IMAGE_FILE_HEADER NativeStruct;

    public FileHeader(IMAGE_FILE_MACHINE Machine, UInt16 NumberOfSections, UInt32 TimeDateStamp, UInt32 PointerToSymbolTable, UInt32 NumberOfSymbols, UInt16 SizeOfOptionalHeader, IMAGE_FILE_CHARACTERISTICS Characteristics)
    {
        NativeStruct = new IMAGE_FILE_HEADER()
        {
            Machine = (UInt16)Machine,
            NumberOfSections = NumberOfSections,
            TimeDateStamp = TimeDateStamp,
            PointerToSymbolTable = PointerToSymbolTable,
            NumberOfSymbols = NumberOfSymbols,
            SizeOfOptionalHeader = SizeOfOptionalHeader,
            Characteristics = (UInt16)Characteristics
        };
    }

    public FileHeader(IMAGE_FILE_MACHINE Machine, IMAGE_FILE_CHARACTERISTICS AdditionalFileCharacteristics)
    {
        NativeStruct = new IMAGE_FILE_HEADER()
        {
            Machine = (UInt16)Machine,
            NumberOfSections = 0,
            TimeDateStamp = UInt32.MaxValue,
            PointerToSymbolTable = 0,
            NumberOfSymbols = 0,
            SizeOfOptionalHeader = 0,
            Characteristics = (UInt16)AdditionalFileCharacteristics
        };
        if (MachineBits == 32)
        {
            NativeStruct.Characteristics |= (UInt16)IMAGE_FILE_CHARACTERISTICS._32BIT_MACHINE;
        }
    }
    public IMAGE_FILE_MACHINE Machine
    {
        get
        {
            return (IMAGE_FILE_MACHINE)NativeStruct.Machine;
        }
        set
        {
            NativeStruct.Machine = (UInt16)value;
        }
    }

    public Byte MachineBits
    {
        get
        {
            return GetMachineBits(Machine);
        }
    }

    public Byte SizeOfPointer
    {
        get
        {
            return GetSizeOfPointer(Machine);
        }
    }
}

