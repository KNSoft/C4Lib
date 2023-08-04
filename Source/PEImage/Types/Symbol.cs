using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

public enum IMAGE_SYM : Int16
{
    UNDEFINED = 0,
    ABSOLUTE = 1,
    DEBUG = 2
}

public enum IMAGE_SYM_CLASS : SByte
{
    EXTERNAL = 0x0002,
    STATIC = 0x0003,
    SECTION = 0x0068
}

/* MSB */
public enum IMAGE_SYM_TYPE : Byte
{
    NULL = 0
}

/* LSB */
public enum IMAGE_SYM_DTYPE : Byte
{
    NULL = 0,
    FUNCTION = 2
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
    public IMAGE_SYMBOL NativeStruct;

    public Symbol(Byte[] NameBytes, UInt32 Value, Int16 SectionNumber, IMAGE_SYM_TYPE TypeMSB, IMAGE_SYM_DTYPE TypeLSB,IMAGE_SYM_CLASS StorageClass)
    {
        NativeStruct = new IMAGE_SYMBOL()
        {
            ShortName = NameBytes,
            Value = Value,
            SectionNumber = SectionNumber,
            TypeMSB = (Byte)TypeMSB,
            TypeLSB = (Byte)TypeLSB,
            StorageClass = (SByte)StorageClass,
            NumberOfAuxSymbols = 0
        };
    }

    public static Byte[] GetNameBytes(UInt32 NameOffset)
    {
        return Rtl.CombineArray(BitConverter.GetBytes((UInt32)0), BitConverter.GetBytes(NameOffset));
    }

    public static Byte[]? GetNameBytes(String Name)
    {
        Byte[] NameBytes = new Byte[8];
        Byte[] SourceNameBytes = Encoding.UTF8.GetBytes(Name);

        if (SourceNameBytes.Length > NameBytes.Length)
        {
            return null;
        } 

        SourceNameBytes.CopyTo(NameBytes, 0);

        return NameBytes;
    }
}

