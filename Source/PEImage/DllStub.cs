using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

public static class DllStub
{
    public class DllExport
    {
        public IMPORT_OBJECT_TYPE Type;
        public String Name;
        public IMPORT_OBJECT_NAME_TYPE NameType;

        public DllExport(IMPORT_OBJECT_TYPE Type, String Name, IMPORT_OBJECT_NAME_TYPE NameType)
        {
            this.Type = Type;
            this.Name = Name;
            this.NameType = NameType;
        }
    }

    private static void WriteBigEndian(Stream Output, UInt32 Number)
    {
        Byte[] Bytes = BitConverter.GetBytes(Number);
        Array.Reverse(Bytes);
        Rtl.WriteToStream(Output, Bytes);
    }

    private struct MakeImportLibraryFile_Import
    {
        public Byte[] Data;
        public UInt32 Offset;
        public String[] Names;
        public String LibName;
        public UInt32? LibLongnameOffset;

        public MakeImportLibraryFile_Import(Byte[] Data, String[] Names, String LibName, UInt32? LibLongnameOffset, UInt32 Offset)
        {
            this.Data = Data;
            this.Offset = Offset;
            this.Names = Names;
            this.LibName = LibName;
            this.LibLongnameOffset = LibLongnameOffset;
        }
    }

    private struct MakeImportLibraryFile_Symbol
    {
        public String Name;
        public Byte[] Data;
        public UInt32 Offset;
        public UInt32 ImportIndex;

        public MakeImportLibraryFile_Symbol(String Name, Byte[] Data, UInt32 Offset, UInt32 ImportIndex)
        {
            this.Name = Name;
            this.Data = Data;
            this.Offset = Offset;
            this.ImportIndex = ImportIndex;
        }
    }

    private static UInt32 MakeImportStubLibraryFile_AddImport(List<MakeImportLibraryFile_Import> Imports, MakeImportLibraryFile_Import Import)
    {
        Imports.Add(Import);
        return (UInt32)(Marshal.SizeOf<IMAGE_ARCHIVE_MEMBER_HEADER>() +
                        Import.Data.Length +
                        Import.Data.Length % 2); // Padding
    }

    public static Byte[] MakeImportStubLibraryFile(IMAGE_FILE_MACHINE Machine, List<KeyValuePair<String, List<DllExport>>> DllExports)
    {
        MemoryStream Output = new();
        MemoryStream ObjectStream = new();
        UInt32 Offset = 0;
        List<MakeImportLibraryFile_Import> Imports = new();
        List<String> Longnames = new();
        String[] ObjectSymbolNames;

        /* __NULL_IMPORT_DESCRIPTOR object file */
        ObjectSymbolNames = new[] {
            ObjectFile.WriteNullIIDObjectFile(ObjectStream, Machine)
        };
        Offset += MakeImportStubLibraryFile_AddImport(Imports, new(Rtl.GetStreamBytes(ObjectStream, 0, ObjectStream.Position),
                                                                   ObjectSymbolNames,
                                                                   "Precomp4C",
                                                                   null,
                                                                   0));
        ObjectStream.Dispose();

        UInt32 LongnamesSize = 0;
        foreach (var DllExport in DllExports)
        {
            /* Longnames */
            UInt32? LongNameOffset;
            if (DllExport.Key.Length > ArchiveMemberHeader.MaxShortNameLength)
            {
                Longnames.Add(DllExport.Key);
                LongNameOffset = LongnamesSize;
                LongnamesSize += (UInt32)DllExport.Key.Length + 1;
            } else
            {
                LongNameOffset = null;
            }

            /* Stub symbols for DLL */
            ObjectStream = new();
            ObjectSymbolNames = ObjectFile.WriteDllImportStubObjectFile(ObjectStream, Machine, DllExport.Key);
            Offset += MakeImportStubLibraryFile_AddImport(Imports, new(Rtl.GetStreamBytes(ObjectStream, 0, ObjectStream.Position),
                                                                       ObjectSymbolNames,
                                                                       DllExport.Key,
                                                                       LongNameOffset,
                                                                       Offset));
            ObjectStream.Dispose();

            /* Dll exports */
            foreach (var Export in DllExport.Value)
            {
                Offset += MakeImportStubLibraryFile_AddImport(Imports, new(new ImportObjectHeader(Machine,
                                                                                                  Export.Type,
                                                                                                  Export.NameType,
                                                                                                  DllExport.Key,
                                                                                                  Export.Name).Bytes,
                                                                           new[] {
                                                                               Export.Name,
                                                                               "__imp_" + Export.Name
                                                                           },
                                                                           DllExport.Key,
                                                                           LongNameOffset,
                                                                           Offset));
            }
        }
        ArchiveMemberHeader? LongnamesAmh = LongnamesSize > 0 ? new(ArchiveMemberHeader.LongNamesMemberName, LongnamesSize) : null;

        /* Transform import list to symbol list */
        UInt32 StringTableSize = 0;
        List<MakeImportLibraryFile_Symbol> Symbols = new();
        for (Int32 i = 0; i < Imports.Count; i++)
        {
            foreach (String ImportName in Imports[i].Names)
            {
                StringTableSize += (UInt32)ImportName.Length + 1;
                Symbols.Add(new(ImportName, Imports[i].Data, Imports[i].Offset, (UInt32)i));
            }
        }
        List<MakeImportLibraryFile_Symbol> SortedSymbols = new(Symbols);
        SortedSymbols.Sort((a, b) => a.Name.CompareTo(b.Name));

        /* Calculate sizes */
        UInt32 FirstSize = sizeof(UInt32) +                             // Number of Symbols
                           (UInt32)Symbols.Count * sizeof(UInt32) +     // Offsets
                           StringTableSize;                             // String Table

        UInt32 SecondSize = sizeof(UInt32) * 2 +                        // Number of Members, Number of Symbols
                            (UInt32)Imports.Count * sizeof(UInt32) +    // Offsets
                            (UInt32)Symbols.Count * sizeof(UInt16) +    // Indices
                            StringTableSize;                            // String Table

        ArchiveMemberHeader FirstAmh = new(ArchiveMemberHeader.LinkerMemberName, FirstSize), SecondAmh = new(ArchiveMemberHeader.LinkerMemberName, SecondSize);

        /* Calculate offsets */
        Offset = (UInt32)(ArchiveFile.Start.Length +
                          FirstAmh.Bytes.Length + FirstSize + (FirstAmh.Size % 2) +
                          SecondAmh.Bytes.Length + SecondSize + (SecondAmh.Size % 2));
        if (LongnamesAmh != null)
        {
            Offset += (UInt32)LongnamesAmh.Bytes.Length + LongnamesSize + (LongnamesAmh.Size % 2);
        }

        /* Archive File Signature */
        Rtl.WriteToStream(Output, ArchiveFile.Start);

        /* First Linker Member */
        Rtl.WriteToStream(Output, FirstAmh.Bytes);
        WriteBigEndian(Output, (UInt32)Symbols.Count);
        foreach (var Symbol in Symbols)
        {
            WriteBigEndian(Output, Offset + Symbol.Offset);
        }
        foreach (var Symbol in Symbols)
        {
            Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(Symbol.Name + '\0'));
        }
        if (FirstAmh.Size % 2 != 0)
        {
            Output.WriteByte(ArchiveFile.Pad);
        }

        /* Second Linker Member */
        Rtl.WriteToStream(Output, SecondAmh.Bytes);
        Rtl.WriteToStream(Output, BitConverter.GetBytes((UInt32)Imports.Count));
        foreach (var Import in Imports)
        {
            Rtl.WriteToStream(Output, BitConverter.GetBytes(Offset + Import.Offset));
        }
        Rtl.WriteToStream(Output, BitConverter.GetBytes((UInt32)Symbols.Count));
        foreach (var Symbol in SortedSymbols)
        {
            Rtl.WriteToStream(Output, BitConverter.GetBytes((UInt16)(Symbol.ImportIndex + 1)));
        }
        foreach (var Symbol in SortedSymbols)
        {
            Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(Symbol.Name + '\0'));
        }
        if (SecondAmh.Size % 2 != 0)
        {
            Output.WriteByte(ArchiveFile.Pad);
        }

        /* Longnames Member */
        if (LongnamesAmh != null)
        {
            Rtl.WriteToStream(Output, LongnamesAmh.Bytes);
            foreach (String Longname in Longnames)
            {
                Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(Longname + '\0'));
            }
            if (LongnamesAmh.Size % 2 != 0)
            {
                Output.WriteByte(ArchiveFile.Pad);
            }
        }

        /* Import Headers */
        foreach (var Import in Imports)
        {
            Rtl.WriteToStream(Output, new ArchiveMemberHeader(
                Import.LibLongnameOffset == null ? Import.LibName : (ArchiveMemberHeader.LongNameMemberNamePrefix + Import.LibLongnameOffset.ToString()),
                (UInt32)Import.Data.Length).Bytes);
            Rtl.WriteToStream(Output, Import.Data);
            if (Import.Data.Length % 2 != 0)
            {
                Output.WriteByte(ArchiveFile.Pad);
            }
        }

        Byte[] Bytes = Rtl.ResizeArray(Output.GetBuffer(), (Int32)Output.Position);
        Output.Dispose();
        return Bytes;
    }
}
