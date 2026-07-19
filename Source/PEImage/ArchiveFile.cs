using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

public class ArchiveFile
{
    public static readonly Byte[] Start = "!<arch>\n"u8.ToArray();
    public static readonly Byte[] End = [0x60, 0x0A];
    public static readonly Byte Pad = 0x0A;

    public class Import
    {
        public required ArchiveMemberHeader Header;
        public required String Name;
        public required String[] SymbolNames;
        public required String[] ECSymbolNames;
        public UInt32 Offset; // On write, relative to the first symbol; on read, absolute file offset
        public required Byte[] Data;
    }

    private readonly List<KeyValuePair<UInt32, Byte[]>> LongnamesTable = []; // Offset in Longnames -> String

    public readonly List<Import> Imports = [];

    /* Create new */
    public ArchiveFile() { }

    /* Load existing */
    public ArchiveFile(Byte[] RawData)
    {
        if (!Rtl.ArrayResize(RawData, Start.Length).SequenceEqual(Start))
        {
            throw new InvalidDataException();
        }

        ArchiveMemberHeader amh;
        ASCIIEncoding ASCIIEnc = new ASCIIEncoding();
        Int32 Offset, EndOffset, StartOffset, StartIndex;

        /* Skip 1st Linker Member */
        amh = new(Rtl.ArraySlice(RawData, Start.Length, Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>()));
        if (!amh.NativeStruct.Name.SequenceEqual(ArchiveMemberHeader.LinkerMemberName))
        {
            throw new InvalidDataException();
        }
        Offset = Start.Length + Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>() + (Int32)amh.Size;
        Offset += Offset % 2;

        /* Read 2nd Linker Member */
        amh = new(Rtl.ArraySlice(RawData, Offset, Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>()));
        if (!amh.NativeStruct.Name.SequenceEqual(ArchiveMemberHeader.LinkerMemberName))
        {
            throw new InvalidDataException();
        }
        Offset += Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>();
        EndOffset = Offset + (Int32)amh.Size;

        /* Members */
        UInt32 MemberCount = BitConverter.ToUInt32(RawData, Offset);
        Offset += sizeof(UInt32);
        List<UInt32> MemberOffsets = [];
        UInt32 MemberOffset, PrevMemberOffset = 0;
        for (UInt32 i = 0; i < MemberCount; i++)
        {
            MemberOffset = BitConverter.ToUInt32(RawData, Offset);
            if (MemberOffset < PrevMemberOffset)
            {
                throw new InvalidDataException();
            }
            MemberOffsets.Add(MemberOffset);
            PrevMemberOffset = MemberOffset;
            Offset += sizeof(UInt32);
        }

        /* Symbols */
        UInt32 SymbolCount = BitConverter.ToUInt32(RawData, Offset);
        List<UInt16> SymbolIndices = [];
        UInt16 SymbolIndice;
        Offset += sizeof(UInt32);
        for (UInt32 i = 0; i < SymbolCount; i++)
        {
            SymbolIndice = BitConverter.ToUInt16(RawData, Offset);
            if (SymbolIndice == 0 || SymbolIndice > MemberOffsets.Count)
            {
                throw new InvalidDataException();
            }
            SymbolIndices.Add(SymbolIndice);
            Offset += sizeof(UInt16);
        }

        /* Strings */
        List<String> Strings = [];
        StartIndex = Offset;
        while (Offset < EndOffset)
        {
            if (RawData[Offset] == '\0')
            {
                if (Strings.Count >= SymbolCount)
                {
                    throw new InvalidDataException();
                }
                Strings.Add(ASCIIEnc.GetString(RawData, StartIndex, Offset - StartIndex));
                StartIndex = Offset + 1;
            }
            Offset++;
        }
        Offset += Offset % 2;

        /* Longnames Member */
        amh = new(Rtl.ArraySlice(RawData, Offset, Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>()));
        if (amh.NativeStruct.Name.SequenceEqual(ArchiveMemberHeader.LongNamesMemberName))
        {
            Offset += Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>();
            EndOffset = Offset + (Int32)amh.Size;
            StartOffset = StartIndex = Offset;
            while (Offset < EndOffset)
            {
                if (RawData[Offset] == '\0')
                {
                    LongnamesTable.Add(new((UInt32)(StartIndex - StartOffset), Rtl.ArraySlice(RawData, StartIndex, Offset - StartIndex)));
                    StartIndex = Offset + 1;
                }
                Offset++;
            }
            Offset += Offset % 2;
        }

        /* ARM64EC Symbols Member */
        List<UInt16> ECSymbolIndices = [];
        List<String> ECSymbolNames = [];
        amh = new(Rtl.ArraySlice(RawData, Offset, Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>()));
        if (amh.NativeStruct.Name.SequenceEqual(ArchiveMemberHeader.ECSymbolsMemberName))
        {
            Offset += Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>();
            EndOffset = Offset + (Int32)amh.Size;

            UInt32 ECSymbolCount = BitConverter.ToUInt32(RawData, Offset);
            Offset += sizeof(UInt32);
            for (UInt32 i = 0; i < ECSymbolCount; i++)
            {
                SymbolIndice = BitConverter.ToUInt16(RawData, Offset);
                if (SymbolIndice == 0 || SymbolIndice > MemberOffsets.Count)
                {
                    throw new InvalidDataException();
                }
                ECSymbolIndices.Add(SymbolIndice);
                Offset += sizeof(UInt16);
            }

            StartIndex = Offset;
            while (Offset < EndOffset)
            {
                if (RawData[Offset] == '\0')
                {
                    if (ECSymbolNames.Count >= ECSymbolCount)
                    {
                        throw new InvalidDataException();
                    }
                    ECSymbolNames.Add(ASCIIEnc.GetString(RawData, StartIndex, Offset - StartIndex));
                    StartIndex = Offset + 1;
                }
                Offset++;
            }
            if (ECSymbolNames.Count != ECSymbolCount)
            {
                throw new InvalidDataException();
            }
        }

        /* Resolve imports */
        for (Int32 i = 0; i < MemberOffsets.Count; i++)
        {
            amh = new(Rtl.ArraySlice(RawData, (Int32)MemberOffsets[i], Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>()));
            List<String> SymbolNames = [];
            for (Int32 j = 0; j < SymbolIndices.Count; j++)
            {
                if (SymbolIndices[j] == (Int16)i + 1)
                {
                    SymbolNames.Add(Strings[j]);
                }
            }
            List<String> ImportECSymbolNames = [];
            for (Int32 j = 0; j < ECSymbolIndices.Count; j++)
            {
                if (ECSymbolIndices[j] == (Int16)i + 1)
                {
                    ImportECSymbolNames.Add(ECSymbolNames[j]);
                }
            }

            String? Name = amh.GetName(out var LongnameOffset);
            Name ??= Encoding.ASCII.GetString(LongnamesTable.Find(x => x.Key == LongnameOffset).Value);

            Imports.Add(new()
            {
                Name = Name,
                Header = amh,
                SymbolNames = [.. SymbolNames],
                ECSymbolNames = [.. ImportECSymbolNames],
                Offset = MemberOffsets[i],
                Data = Rtl.ArraySlice(RawData, (Int32)MemberOffsets[i] + Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>(), (Int32)amh.Size)
            });
        }
    }

    public void AddImport(String ArchiveMemberName, String[] SymbolNames, Byte[] Data)
    {
        AddImport(ArchiveMemberName, SymbolNames, [], Data);
    }

    public void AddImport(String ArchiveMemberName, String[] SymbolNames, String[] ECSymbolNames, Byte[] Data)
    {
        UInt32 Offset;
        Byte[]? NameBytes = ArchiveMemberHeader.GetNameBytes(ArchiveMemberName);

        if (NameBytes == null)
        {
            Offset = LongnamesTable.Count == 0 ? 0 : (LongnamesTable.Last().Key + (UInt32)LongnamesTable.Last().Value.Length + 1);
            LongnamesTable.Add(new(Offset, Encoding.ASCII.GetBytes(ArchiveMemberName)));
            NameBytes = ArchiveMemberHeader.GetNameBytes(Offset);
        }

        if (Imports.Count > 0)
        {
            Offset = Imports.Last().Offset + (UInt32)Marshal.SizeOf(Imports.Last().Header.NativeStruct) + Imports.Last().Header.Size;
            Offset += Offset % 2;
        } else
        {
            Offset = 0;
        }

        Imports.Add(new()
        {
            Name = ArchiveMemberName,
            Header = new ArchiveMemberHeader(NameBytes, (UInt32)Data.Length),
            SymbolNames = SymbolNames,
            ECSymbolNames = ECSymbolNames,
            Offset = Offset,
            Data = Data
        });
    }

    public void AddImport(String ArchiveMemberName, ObjectFile ObjectFile)
    {
        AddImport(ArchiveMemberName, ObjectFile, []);
    }

    public void AddImport(String ArchiveMemberName, ObjectFile ObjectFile, String[] ECSymbolNames)
    {

        MemoryStream ObjectFileStream = new();

        ObjectFile.Write(ObjectFileStream);
        AddImport(ArchiveMemberName, [.. ObjectFile.SymbolNames], ECSymbolNames, ObjectFileStream.ToArray());

        ObjectFileStream.Dispose();
    }

    public void AddImport(IMAGE_FILE_MACHINE Machine, UInt16 OrdinalOrHint, IMPORT_OBJECT_TYPE Type, IMPORT_OBJECT_NAME_TYPE NameType, String DllName, String ImportName)
    {
        String ObjectImportName = ImportName;
        String? ExportAsName = null;
        String[] SymbolNames;
        String[] ECSymbolNames;

        if (Machine == IMAGE_FILE_MACHINE.ARM64EC)
        {
            SymbolNames = [];
            if (Type == IMPORT_OBJECT_TYPE.CODE)
            {
                ObjectImportName = '#' + ImportName;
                ECSymbolNames =
                [
                    ObjectImportName,
                    ImportName,
                    "__imp_" + ImportName,
                    "__imp_aux_" + ImportName
                ];
                if (NameType != IMPORT_OBJECT_NAME_TYPE.ORDINAL)
                {
                    NameType = IMPORT_OBJECT_NAME_TYPE.NAME_EXPORTAS;
                    ExportAsName = ImportName;
                }
            } else
            {
                ECSymbolNames = ["__imp_" + ImportName];
            }
        } else
        {
            SymbolNames =
            [
                ImportName,
                "__imp_" + ImportName
            ];
            ECSymbolNames = [];
        }

        Byte[] ImportData = ImportObjectHeader.GetImportNameBuffer(DllName, ObjectImportName, ExportAsName);

        AddImport(DllName,
                  SymbolNames,
                  ECSymbolNames,
                  Rtl.ArrayCombine(Rtl.StructToRaw(new ImportObjectHeader(Machine,
                                                                          (UInt32)ImportData.Length,
                                                                          OrdinalOrHint,
                                                                          Type,
                                                                          NameType).NativeStruct),
                                   ImportData));
    }

    private static void WriteBigEndian(Stream Output, UInt32 Number)
    {
        Byte[] Bytes = BitConverter.GetBytes(Number);
        Array.Reverse(Bytes);
        Rtl.StreamWrite(Output, Bytes);
    }

    public void Write(Stream Output)
    {
        /* Transform import list to symbol list and calculate size of string table */
        UInt32 StringTableSize = 0;
        UInt32 ECStringTableSize = 0;
        List<KeyValuePair<String, Import>> Symbols = [];
        List<KeyValuePair<String, Import>> ECSymbols = [];
        foreach (Import Import in Imports)
        {
            foreach (String SymbolName in Import.SymbolNames)
            {
                Symbols.Add(new(SymbolName, Import));
                StringTableSize += (UInt32)SymbolName.Length + 1;
            }
            foreach (String SymbolName in Import.ECSymbolNames)
            {
                ECSymbols.Add(new(SymbolName, Import));
                ECStringTableSize += (UInt32)SymbolName.Length + 1;
            }
        }
        List<KeyValuePair<String, Import>> SortedSymbols = [.. Symbols];
        SortedSymbols.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        List<KeyValuePair<String, Import>> SortedECSymbols = [.. ECSymbols];
        SortedECSymbols.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));

        /* Archive File Signature */
        Rtl.StreamWrite(Output, Start);

        /* Linker Members */
        ArchiveMemberHeader FirstAmh = new(ArchiveMemberHeader.LinkerMemberName, sizeof(UInt32) +                           // Number of Symbols
                                                                                 (UInt32)Symbols.Count * sizeof(UInt32) +   // Offsets
                                                                                 StringTableSize                            // String Table
                                           );
        ArchiveMemberHeader SecondAmh = new(ArchiveMemberHeader.LinkerMemberName, sizeof(UInt32) * 2 +                      // Number of Members, Number of Symbols
                                                                                  (UInt32)Imports.Count * sizeof(UInt32) +  // Offsets
                                                                                  (UInt32)Symbols.Count * sizeof(UInt16) +  // Indices
                                                                                  StringTableSize                           // String Table
                                            );
        ArchiveMemberHeader? LongnamesAmh = LongnamesTable.Count > 0 ? new(ArchiveMemberHeader.LongNamesMemberName,
                                                                           (UInt32)(LongnamesTable.Sum(x => x.Value.Length) + LongnamesTable.Count)) : null;
        ArchiveMemberHeader? ECSymbolsAmh = ECSymbols.Count > 0 ? new(ArchiveMemberHeader.ECSymbolsMemberName,
                                                                       sizeof(UInt32) +
                                                                       (UInt32)ECSymbols.Count * sizeof(UInt16) +
                                                                       ECStringTableSize) : null;

        /* Calculate import offsets */
        UInt32 Offset = (UInt32)(Start.Length +
                                 Marshal.SizeOf(FirstAmh.NativeStruct) + FirstAmh.Size + (FirstAmh.Size % 2) +
                                 Marshal.SizeOf(SecondAmh.NativeStruct) + SecondAmh.Size + (SecondAmh.Size % 2));
        if (LongnamesAmh != null)
        {
            Offset += (UInt32)Marshal.SizeOf(LongnamesAmh.NativeStruct) + LongnamesAmh.Size + (LongnamesAmh.Size % 2);
        }
        if (ECSymbolsAmh != null)
        {
            Offset += (UInt32)Marshal.SizeOf(ECSymbolsAmh.NativeStruct) + ECSymbolsAmh.Size + (ECSymbolsAmh.Size % 2);
        }

        /* Write First Linker Member */
        Rtl.StreamWrite(Output, Rtl.StructToRaw(FirstAmh.NativeStruct));
        WriteBigEndian(Output, (UInt32)Symbols.Count);
        foreach (var Symbol in Symbols)
        {
            WriteBigEndian(Output, Offset + Symbol.Value.Offset);
        }
        foreach (var Symbol in Symbols)
        {
            Rtl.StreamWrite(Output, Encoding.ASCII.GetBytes(Symbol.Key + '\0'));
        }
        if (FirstAmh.Size % 2 != 0)
        {
            Output.WriteByte(Pad);
        }

        /* Second Linker Member */
        Rtl.StreamWrite(Output, Rtl.StructToRaw(SecondAmh.NativeStruct));
        Rtl.StreamWrite(Output, BitConverter.GetBytes((UInt32)Imports.Count));
        foreach (Import Import in Imports)
        {
            Rtl.StreamWrite(Output, BitConverter.GetBytes(Offset + Import.Offset));
        }
        Rtl.StreamWrite(Output, BitConverter.GetBytes((UInt32)Symbols.Count));
        foreach (var Symbol in SortedSymbols)
        {
            Int32 ImportIndex = Imports.IndexOf(Symbol.Value);
            if (ImportIndex < 0)
            {
                throw new IndexOutOfRangeException();
            }
            Rtl.StreamWrite(Output, BitConverter.GetBytes((UInt16)(ImportIndex + 1)));
        }
        foreach (var Symbol in SortedSymbols)
        {
            Rtl.StreamWrite(Output, Encoding.ASCII.GetBytes(Symbol.Key + '\0'));
        }
        if (SecondAmh.Size % 2 != 0)
        {
            Output.WriteByte(Pad);
        }

        /* Longnames Member */
        if (LongnamesAmh != null)
        {
            Rtl.StreamWrite(Output, Rtl.StructToRaw(LongnamesAmh.NativeStruct));
            foreach (var Longname in LongnamesTable)
            {
                Rtl.StreamWrite(Output, Longname.Value);
                Output.WriteByte(0);
            }
            if (LongnamesAmh.Size % 2 != 0)
            {
                Output.WriteByte(Pad);
            }
        }

        /* ARM64EC Symbols Member */
        if (ECSymbolsAmh != null)
        {
            Rtl.StreamWrite(Output, Rtl.StructToRaw(ECSymbolsAmh.NativeStruct));
            Rtl.StreamWrite(Output, BitConverter.GetBytes((UInt32)SortedECSymbols.Count));
            foreach (var Symbol in SortedECSymbols)
            {
                Int32 ImportIndex = Imports.IndexOf(Symbol.Value);
                if (ImportIndex < 0)
                {
                    throw new IndexOutOfRangeException();
                }
                Rtl.StreamWrite(Output, BitConverter.GetBytes((UInt16)(ImportIndex + 1)));
            }
            foreach (var Symbol in SortedECSymbols)
            {
                Rtl.StreamWrite(Output, Encoding.ASCII.GetBytes(Symbol.Key + '\0'));
            }
            if (ECSymbolsAmh.Size % 2 != 0)
            {
                Output.WriteByte(Pad);
            }
        }

        /* Import headers and data */
        foreach (Import Import in Imports)
        {
            Rtl.StreamWrite(Output, Rtl.StructToRaw(Import.Header.NativeStruct));
            Rtl.StreamWrite(Output, Import.Data);
            if (Import.Data.Length % 2 != 0)
            {
                Output.WriteByte(Pad);
            }
        }
    }
}
