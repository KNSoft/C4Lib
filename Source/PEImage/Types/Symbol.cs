using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

public enum IMAGE_SYM_CLASS : SByte
{
    EXTERNAL = 0x0002,
    STATIC = 0x0003,
    SECTION = 0x0068
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct IMAGE_SYMBOL
{
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 8)]
    [FieldOffset(0)]
    public Byte[] ShortName;
    [FieldOffset(8)]
    public UInt32 Value;
    [FieldOffset(12)]
    public Int16 SectionNumber;
    [FieldOffset(14)]
    public UInt16 Type;
    [FieldOffset(14)]
    public Byte TypeMSB;
    [FieldOffset(15)]
    public Byte TypeLSB;
    [FieldOffset(16)]
    public SByte StorageClass;
    [FieldOffset(17)]
    public Byte NumberOfAuxSymbols;
}

public class Symbol
{
    public Byte[] Bytes;

    public Symbol(String Name, UInt32 Value, Int16 SectionNumber, UInt16 Type, IMAGE_SYM_CLASS StorageClass)
    {
        String Text = (Name.Length > 8 ? Name[..8] : Name);
        Bytes = Rtl.StructToRaw(new IMAGE_SYMBOL()
        {
            ShortName = Encoding.ASCII.GetBytes(Text + new String('\0', 8 - Text.Length)),
            Value = Value,
            SectionNumber = SectionNumber,
            Type = Type,
            StorageClass = (SByte)StorageClass,
            NumberOfAuxSymbols = 0
        });
    }

    public Symbol(UInt32 NameIndex, UInt32 Value, Int16 SectionNumber, UInt16 Type, IMAGE_SYM_CLASS StorageClass)
    {
        Bytes = Rtl.StructToRaw(new IMAGE_SYMBOL()
        {
            ShortName = Rtl.CombineArray(BitConverter.GetBytes((UInt32)0), BitConverter.GetBytes(NameIndex)),
            Value = Value,
            SectionNumber = SectionNumber,
            Type = Type,
            StorageClass = (SByte)StorageClass,
            NumberOfAuxSymbols = 0
        });
    }
}

