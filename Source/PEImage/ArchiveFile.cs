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
    public static readonly Byte[] End = { 0x60, 0x0A };
    public static readonly Byte Pad = 0x0A;

    public IMAGE_FILE_MACHINE Machine;

    public readonly List<KeyValuePair<UInt32, Byte[]>> LongnamesTable = new(); // Offset in Longnames -> String

    public class Import
    {
        public required ArchiveMemberHeader Header;
        public required String[] SymbolNames;
        public UInt32 Offset; // Relative to the first symbol
        public required Byte[] Data;
    }

    private readonly List<Import> Imports = new();

    public ArchiveFile(IMAGE_FILE_MACHINE Machine)
    {
        this.Machine = Machine;
    }

    public void AddImport(String ArchiveMemberName, String[] SymbolNames, Byte[] Data)
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
            Header = new ArchiveMemberHeader(NameBytes, (UInt32)Data.Length),
            SymbolNames = SymbolNames,
            Offset = Offset,
            Data = Data
        });
    }

    public void AddImport(String ArchiveMemberName, ObjectFile ObjectFile)
    {

        MemoryStream ObjectFileStream = new();

        ObjectFile.Write(ObjectFileStream);
        AddImport(ArchiveMemberName, ObjectFile.SymbolNames.ToArray(), ObjectFileStream.ToArray());
    }

    public void AddImport(IMPORT_OBJECT_TYPE Type, IMPORT_OBJECT_NAME_TYPE NameType, String DllName, String ImportName)
    {
        Byte[] ImportData = ImportObjectHeader.GetImportNameBuffer(DllName, ImportName);

        AddImport(DllName,
                  new[] {
                      ImportName,
                      "__imp_" + ImportName
                  },
                  Rtl.CombineArray(Rtl.StructToRaw(new ImportObjectHeader(Machine,
                                                                          (UInt32)ImportData.Length,
                                                                          0,
                                                                          Type,
                                                                          NameType).NativeStruct),
                                   ImportData));
    }

    private static void WriteBigEndian(Stream Output, UInt32 Number)
    {
        Byte[] Bytes = BitConverter.GetBytes(Number);
        Array.Reverse(Bytes);
        Rtl.WriteToStream(Output, Bytes);
    }

    public void Write(Stream Output)
    {
        /* Transform import list to symbol list and calculate size of string table */
        UInt32 StringTableSize = 0;
        List<KeyValuePair<String, Import>> Symbols = new();
        foreach (Import Import in Imports)
        {
            foreach (String SymbolName in Import.SymbolNames)
            {
                Symbols.Add(new(SymbolName, Import));
                StringTableSize += (UInt32)SymbolName.Length + 1;
            }
        }
        List<KeyValuePair<String, Import>> SortedSymbols = new(Symbols);
        SortedSymbols.Sort((a, b) => a.Key.CompareTo(b.Key));

        /* Archive File Signature */
        Rtl.WriteToStream(Output, Start);

        /* Linker Members */
        ArchiveMemberHeader FirstAmh = new(ArchiveMemberHeader.LinkerMemberName, sizeof(UInt32) +                           // Number of Symbols
                                                                                 (UInt32)Symbols.Count * sizeof(UInt32)     // Offsets
                                                                                 + StringTableSize                          // String Table
                                                                                 );
        ArchiveMemberHeader SecondAmh = new(ArchiveMemberHeader.LinkerMemberName, sizeof(UInt32) * 2 +                      // Number of Members, Number of Symbols
                                                                                  (UInt32)Imports.Count * sizeof(UInt32) +  // Offsets
                                                                                  (UInt32)Symbols.Count * sizeof(UInt16) +  // Indices
                                                                                  StringTableSize                           // String Table
                                                                                  );
        ArchiveMemberHeader? LongnamesAmh = LongnamesTable.Count > 0 ? new(ArchiveMemberHeader.LongNamesMemberName,
                                                                           (UInt32)(LongnamesTable.Sum(x => x.Value.Length) + LongnamesTable.Count)) : null;

        /* Calculate import offsets */
        UInt32 Offset = (UInt32)(Start.Length +
                                 Marshal.SizeOf(FirstAmh.NativeStruct) + FirstAmh.Size + (FirstAmh.Size % 2) +
                                 Marshal.SizeOf(SecondAmh.NativeStruct) + SecondAmh.Size + (SecondAmh.Size % 2));
        if (LongnamesAmh != null)
        {
            Offset += (UInt32)Marshal.SizeOf(LongnamesAmh.NativeStruct) + LongnamesAmh.Size + (LongnamesAmh.Size % 2);
        }

        /* Write First Linker Member */
        Rtl.WriteToStream(Output, Rtl.StructToRaw(FirstAmh.NativeStruct));
        WriteBigEndian(Output, (UInt32)Symbols.Count);
        foreach (var Symbol in Symbols)
        {
            WriteBigEndian(Output, Offset + Symbol.Value.Offset);
        }
        foreach (var Symbol in Symbols)
        {
            Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(Symbol.Key + '\0'));
        }
        if (FirstAmh.Size % 2 != 0)
        {
            Output.WriteByte(Pad);
        }

        /* Second Linker Member */
        Rtl.WriteToStream(Output, Rtl.StructToRaw(SecondAmh.NativeStruct));
        Rtl.WriteToStream(Output, BitConverter.GetBytes((UInt32)Imports.Count));
        foreach (Import Import in Imports)
        {
            Rtl.WriteToStream(Output, BitConverter.GetBytes(Offset + Import.Offset));
        }
        Rtl.WriteToStream(Output, BitConverter.GetBytes((UInt32)Symbols.Count));
        foreach (var Symbol in SortedSymbols)
        {
            Int32 ImportIndex = Imports.IndexOf(Symbol.Value);
            if (ImportIndex < 0)
            {
                throw new IndexOutOfRangeException();
            }
            Rtl.WriteToStream(Output, BitConverter.GetBytes((UInt16)(ImportIndex + 1)));
        }
        foreach (var Symbol in SortedSymbols)
        {
            Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(Symbol.Key + '\0'));
        }
        if (SecondAmh.Size % 2 != 0)
        {
            Output.WriteByte(Pad);
        }

        /* Longnames Member */
        if (LongnamesAmh != null)
        {
            Rtl.WriteToStream(Output, Rtl.StructToRaw(LongnamesAmh.NativeStruct));
            foreach (var Longname in LongnamesTable)
            {
                Rtl.WriteToStream(Output, Longname.Value);
                Output.WriteByte(0);
            }
            if (LongnamesAmh.Size % 2 != 0)
            {
                Output.WriteByte(Pad);
            }
        }

        /* Import headers and data */
        foreach (Import Import in Imports)
        {
            Rtl.WriteToStream(Output, Rtl.StructToRaw(Import.Header.NativeStruct));
            Rtl.WriteToStream(Output, Import.Data);
            if (Import.Data.Length % 2 != 0)
            {
                Output.WriteByte(Pad);
            }
        }
    }
}
