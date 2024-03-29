﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

public enum IMPORT_OBJECT_TYPE : Byte
{
    CODE = 0,
    DATA = 1,
    CONST = 2
}

public enum IMPORT_OBJECT_NAME_TYPE : Byte
{
    ORDINAL = 0,
    NAME = 1,
    NAME_NO_PREFIX = 2,
    NAME_UNDECORATE = 3,
    NAME_EXPORTAS = 4
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IMPORT_OBJECT_HEADER
{
    public UInt16 Sig1;
    public UInt16 Sig2;
    public UInt16 Version;
    public UInt16 Machine;
    public UInt32 TimeDateStamp;
    public UInt32 SizeOfData;
    public UInt16 Ordinal;
    public UInt16 Type;
}

public class ImportObjectHeader
{
    public static readonly UInt16 IMPORT_OBJECT_HDR_SIG2 = 0xFFFF;

    public static Byte[] GetImportNameBuffer(String DllName, String DllExportName)
    {
        Byte[] Data;
        Byte[] DllExportNameBytes = Encoding.ASCII.GetBytes(DllExportName);
        Byte[] DllNameBytes = Encoding.ASCII.GetBytes(DllName);

        Data = new Byte[DllExportNameBytes.Length + 1 + DllNameBytes.Length + 1];
        Buffer.BlockCopy(DllExportNameBytes, 0, Data, 0, DllExportNameBytes.Length);
        Data[DllExportNameBytes.Length] = (Byte)'\0';
        Buffer.BlockCopy(DllNameBytes, 0, Data, DllExportNameBytes.Length + 1, DllNameBytes.Length);
        Data[DllExportNameBytes.Length + 1 + DllNameBytes.Length] = (Byte)'\0';

        return Data;
    }

    public IMPORT_OBJECT_HEADER NativeStruct;

    public ImportObjectHeader(IMAGE_FILE_MACHINE Machine, UInt32 SizeOfData, UInt16 Ordinal, IMPORT_OBJECT_TYPE Type, IMPORT_OBJECT_NAME_TYPE NameType)
    {
        NativeStruct = new IMPORT_OBJECT_HEADER()
        {
            Sig1 = (UInt16)IMAGE_FILE_MACHINE.UNKNOWN,
            Sig2 = IMPORT_OBJECT_HDR_SIG2,
            Version = 0,
            Machine = (UInt16)Machine,
            TimeDateStamp = 0,
            SizeOfData = SizeOfData,
            Ordinal = Ordinal,
            Type = (UInt16)(((Byte)Type & 0b11) | (((Byte)NameType & 0b111) << 2))
        };
    }
}
