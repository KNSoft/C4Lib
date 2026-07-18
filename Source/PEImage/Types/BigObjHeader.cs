using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KNSoft.C4Lib.PEImage;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ANON_OBJECT_HEADER_BIGOBJ
{
    public UInt16 Sig1;
    public UInt16 Sig2;
    public UInt16 Version;
    public UInt16 Machine;
    public UInt32 TimeDateStamp;
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 16)]
    public Byte[] ClassID;
    public UInt32 SizeOfData;
    public UInt32 Flags;
    public UInt32 MetaDataSize;
    public UInt32 MetaDataOffset;
    public UInt32 NumberOfSections;
    public UInt32 PointerToSymbolTable;
    public UInt32 NumberOfSymbols;
}

public class BigObjHeader
{
    public const UInt16 Signature1 = (UInt16)IMAGE_FILE_MACHINE.UNKNOWN;
    public const UInt16 Signature2 = UInt16.MaxValue;

    public static readonly Byte[] ClassID =
    [
        0xC7, 0xA1, 0xBA, 0xD1, 0xEE, 0xBA, 0xA9, 0x4B,
        0xAF, 0x20, 0xFA, 0xF6, 0x6A, 0xA4, 0xDC, 0xB8
    ];

    public static readonly Int32 HeaderSize = Marshal.SizeOf<ANON_OBJECT_HEADER_BIGOBJ>();

    private static readonly Int32 ClassIDOffset = Marshal.OffsetOf<ANON_OBJECT_HEADER_BIGOBJ>(nameof(ANON_OBJECT_HEADER_BIGOBJ.ClassID)).ToInt32();

    public ANON_OBJECT_HEADER_BIGOBJ NativeStruct;

    public BigObjHeader(Byte[] RawData)
    {
        if (!IsBigObj(RawData))
        {
            throw new InvalidDataException();
        }
        NativeStruct = Rtl.RawToStruct<ANON_OBJECT_HEADER_BIGOBJ>(Rtl.ArraySlice(RawData, 0, HeaderSize));
    }

    public static Boolean HasSignature(Byte[] RawData)
    {
        return RawData.Length >= sizeof(UInt16) * 2 &&
               BitConverter.ToUInt16(RawData, 0) == Signature1 &&
               BitConverter.ToUInt16(RawData, sizeof(UInt16)) == Signature2;
    }

    public static Boolean IsBigObj(Byte[] RawData)
    {
        if (RawData.Length < HeaderSize || !HasSignature(RawData))
        {
            return false;
        }

        for (Int32 i = 0; i < ClassID.Length; i++)
        {
            if (RawData[ClassIDOffset + i] != ClassID[i])
            {
                return false;
            }
        }
        return true;
    }
}
