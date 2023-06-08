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

    private static String[] GetImportStubSymbolNames(String DllName)
    {
        String DllShortName = Path.HasExtension(DllName) ? Path.ChangeExtension(DllName, null) : DllName;

        return new[]
        {
            /* 0 */ "__IMPORT_DESCRIPTOR_" + DllShortName,
            /* 1 */ '\x7F' + DllShortName + "_NULL_THUNK_DATA"
        };
    }

    private struct MakeObjectFile_Section
    {
        public String Name;
        public IMAGE_SCN Align;
        public Byte[] Data;
        public Relocation[]? Reloc;

        public MakeObjectFile_Section(String Name, IMAGE_SCN Align, Byte[] Data, Relocation[]? Reloc)
        {
            this.Name = Name;
            this.Align = Align;
            this.Data = Data;
            this.Reloc = Reloc;
        }
    }

    private static readonly String NullIIDSymbolName = "__NULL_IMPORT_DESCRIPTOR";

    private static Byte[] MakeNullIIDObjectFile(IMAGE_FILE_MACHINE Machine)
    {
        MemoryStream Output = new();

        /* Something depends on machine type */
        IMAGE_FILE_CHARACTERISTICS FileCharacteristics = IMAGE_FILE_CHARACTERISTICS.DEBUG_STRIPPED;
        if (Machine == IMAGE_FILE_MACHINE.I386 || Machine == IMAGE_FILE_MACHINE.ARM)
        {
            FileCharacteristics |= IMAGE_FILE_CHARACTERISTICS._32BIT_MACHINE;
        }

        /* Section and symbol*/
        MakeObjectFile_Section Section = new MakeObjectFile_Section(".idata$3", IMAGE_SCN.ALIGN_4BYTES, new Byte[20], null);
        Symbol Symbol = new(0, 0, 1, 0, IMAGE_SYM_CLASS.EXTERNAL);

        /* Write data */
        UInt32 RawOffset = (UInt32)(Marshal.SizeOf<IMAGE_FILE_HEADER>() + Marshal.SizeOf<IMAGE_SECTION_HEADER>());
        Rtl.WriteToStream(Output, new FileHeader(Machine,
                                                 1,
                                                 RawOffset,
                                                 1,
                                                 FileCharacteristics).Bytes);
        Rtl.WriteToStream(Output, new SectionHeader(Section.Name,
                                                    RawOffset,
                                                    (UInt32)Section.Data.Length,
                                                    0,
                                                    0,
                                                    (IMAGE_SCN)SectionHeader.SCN.idata | Section.Align).Bytes);
        Rtl.WriteToStream(Output, Section.Data);
        Rtl.WriteToStream(Output, Symbol.Bytes);
        UInt32 StringTableSize = sizeof(UInt32) + (UInt32)NullIIDSymbolName.Length + 1;
        Rtl.WriteToStream(Output, BitConverter.GetBytes(StringTableSize));
        Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(NullIIDSymbolName));
        Output.WriteByte(0);

        Byte[] Bytes = Rtl.ResizeArray(Output.GetBuffer(), (Int32)Output.Position);
        Output.Dispose();
        return Bytes;
    }

    public static Byte[] MakeImportStubObjectFile(String DllName, IMAGE_FILE_MACHINE Machine)
    {
        MemoryStream Output = new();
        UInt32 Offset;
        UInt16 RelocType;
        UInt32 SizeOfPointer;

        /* Something depends on machine type */
        IMAGE_FILE_CHARACTERISTICS FileCharacteristics = IMAGE_FILE_CHARACTERISTICS.DEBUG_STRIPPED;
        if (Machine == IMAGE_FILE_MACHINE.I386 || Machine == IMAGE_FILE_MACHINE.ARM)
        {
            FileCharacteristics |= IMAGE_FILE_CHARACTERISTICS._32BIT_MACHINE;
            SizeOfPointer = 4;
        } else
        {
            SizeOfPointer = 8;
        }
        if (Machine == IMAGE_FILE_MACHINE.I386)
        {
            RelocType = (UInt16)IMAGE_REL.I386.DIR32NB;
        } else if (Machine == IMAGE_FILE_MACHINE.AMD64)
        {
            RelocType = (UInt16)IMAGE_REL.AMD64.ADDR32NB;
        } else if (Machine == IMAGE_FILE_MACHINE.ARM64)
        {
            RelocType = (UInt16)IMAGE_REL.ARM64.ADDR32NB;
        } else if (Machine == IMAGE_FILE_MACHINE.ARM)
        {
            RelocType = (UInt16)IMAGE_REL.ARM.ADDR32NB;
        } else
        {
            throw new NotImplementedException("Unsupported machine type: " + Machine.ToString());
        }

        /* Symbol names of IID and thunk */
        String[] SymbolNames = GetImportStubSymbolNames(DllName);

        /* Build string table */
        List<KeyValuePair<UInt32, String>> StringTable = new();
        Offset = sizeof(UInt32);
        for (Int32 i = 0; i < SymbolNames.Length; i++)
        {
            StringTable.Add(new(Offset, SymbolNames[i]));
            Offset += (UInt32)SymbolNames[i].Length + 1;
        }

        /* Relocations in idata2 */
        Relocation[]? idata2Relocs = new Relocation[]
        {
            new(0xC, 2, RelocType),
            new(0x0, 3, RelocType),
            new(0x10, 4, RelocType)
        };

        /* Sections and symbols */
        MakeObjectFile_Section[] Sections =
        {
            /* 1 */ new MakeObjectFile_Section(".idata$2", IMAGE_SCN.ALIGN_4BYTES, new Byte[20], idata2Relocs),
            /* 2 */ new MakeObjectFile_Section(".idata$6", IMAGE_SCN.ALIGN_2BYTES, Encoding.ASCII.GetBytes(DllName + (DllName.Length % 2 == 0 ? "\0\0" : "\0")), null),
            /* 3 */ new MakeObjectFile_Section(".idata$5", IMAGE_SCN.ALIGN_4BYTES, new Byte[SizeOfPointer], null),
            /* 4 */ new MakeObjectFile_Section(".idata$4", IMAGE_SCN.ALIGN_4BYTES, new Byte[SizeOfPointer], null)
        };
        Symbol[] Symbols =
        {
            /* 0 */ new Symbol(StringTable[0].Key, 0, 1, 0, IMAGE_SYM_CLASS.EXTERNAL),                      // __IMPORT_DESCRIPTOR_XXX
            /* 1 */ new Symbol(".idata$2", (UInt32)SectionHeader.SCN.idata, 1, 0, IMAGE_SYM_CLASS.SECTION), // Section of __IMPORT_DESCRIPTOR_XXX
            /* 2 */ new Symbol(".idata$6", 0, 2, 0, IMAGE_SYM_CLASS.STATIC),                                // Section of "XXX.dll"
            /* 3 */ new Symbol(".idata$4", (UInt32)SectionHeader.SCN.idata, 4, 0, IMAGE_SYM_CLASS.SECTION), // Section of Thunk?
            /* 4 */ new Symbol(".idata$5", (UInt32)SectionHeader.SCN.idata, 3, 0, IMAGE_SYM_CLASS.SECTION), // Section of \x7FXXX_NULL_THUNK_DATA
            /* 5 */ new Symbol(StringTable[1].Key, 0, 3, 0, IMAGE_SYM_CLASS.EXTERNAL)                       // \x7FXXX_NULL_THUNK_DATA
        };

        /* Write object file header */
        UInt32 RawOffset = (UInt32)(Marshal.SizeOf<IMAGE_FILE_HEADER>() + Marshal.SizeOf<IMAGE_SECTION_HEADER>() * Sections.Length);
        UInt32 RawSize = 0;
        foreach (var Section in Sections)
        {
            RawSize += (UInt32)Section.Data.Length;
            if (Section.Reloc != null)
            {
                RawSize += (UInt32)(Marshal.SizeOf<IMAGE_RELOCATION>() * Section.Reloc.Length);
            }
        }
        Rtl.WriteToStream(Output, new FileHeader(Machine,
                                                 (UInt16)Sections.Length,
                                                 RawOffset + RawSize,
                                                 (UInt32)Symbols.Length,
                                                 FileCharacteristics).Bytes);

        /* Write section headers */
        Offset = RawOffset;
        foreach (var Section in Sections)
        {
            UInt32 RelocOffset;
            UInt16 RelocCount;
            if (Section.Reloc == null)
            {
                RelocOffset = RelocCount = 0;
            } else
            {
                RelocOffset = Offset + (UInt32)Section.Data.Length;
                RelocCount = (UInt16)Section.Reloc.Length;
            }
            Rtl.WriteToStream(Output, new SectionHeader(Section.Name,
                                                        Offset,
                                                        (UInt32)Section.Data.Length,
                                                        RelocOffset,
                                                        RelocCount,
                                                        (IMAGE_SCN)SectionHeader.SCN.idata | Section.Align).Bytes);
            Offset += (UInt32)Section.Data.Length + (UInt32)Marshal.SizeOf<IMAGE_RELOCATION>() * RelocCount;
        }

        /* Write raw section data */
        foreach (var Section in Sections)
        {
            Rtl.WriteToStream(Output, Section.Data);
            if (Section.Reloc != null)
            {
                foreach (Relocation Reloc in Section.Reloc)
                {
                    Rtl.WriteToStream(Output, Reloc.Bytes);
                }
            }
        }

        /* Write symbols */
        foreach (Symbol Symbol in Symbols)
        {
            Rtl.WriteToStream(Output, Symbol.Bytes);
        }

        /* Write string table */
        RawSize = sizeof(UInt32);
        foreach (String StringItem in SymbolNames)
        {
            RawSize += (UInt32)StringItem.Length + 1;
        }
        Rtl.WriteToStream(Output, BitConverter.GetBytes(RawSize));
        foreach (String Item in SymbolNames)
        {
            Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(Item));
            Output.WriteByte(0);
        }

        Byte[] Bytes = Rtl.ResizeArray(Output.GetBuffer(), (Int32)Output.Position);
        Output.Dispose();
        return Bytes;
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
        UInt32 Offset = 0;

        List<MakeImportLibraryFile_Import> Imports = new();
        List<String> Longnames = new();

        /* __NULL_IMPORT_DESCRIPTOR object file */
        Offset += MakeImportStubLibraryFile_AddImport(Imports, new(MakeNullIIDObjectFile(Machine),
                                                                   new[]
                                                                   {
                                                                       NullIIDSymbolName
                                                                   },
                                                                   "KNSoft",
                                                                   null,
                                                                   0));

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
            Offset += MakeImportStubLibraryFile_AddImport(Imports, new(MakeImportStubObjectFile(DllExport.Key, Machine),
                                                                       GetImportStubSymbolNames(DllExport.Key),
                                                                       DllExport.Key,
                                                                       LongNameOffset,
                                                                       Offset));

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
        ArchiveMemberHeader? LongnamesAmh = LongnamesSize > 0 ? new("/", LongnamesSize) : null;

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

        ArchiveMemberHeader FirstAmh = new(String.Empty, FirstSize), SecondAmh = new(String.Empty, SecondSize);

        /* Calculate offsets */
        Offset = (UInt32)(Archive.Start.Length +
                          FirstAmh.Bytes.Length + FirstSize + FirstAmh.PadSize +
                          SecondAmh.Bytes.Length + SecondSize + SecondAmh.PadSize);
        if (LongnamesAmh != null)
        {
            Offset += (UInt32)LongnamesAmh.Bytes.Length + LongnamesSize + LongnamesAmh.PadSize;
        }

        /* Archive File Signature */
        Rtl.WriteToStream(Output, Archive.Start);

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
        if (FirstAmh.PadSize != 0)
        {
            Output.WriteByte(Archive.Pad);
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
        if (SecondAmh.PadSize != 0)
        {
            Output.WriteByte(Archive.Pad);
        }

        /* Longnames Member */
        if (LongnamesAmh != null)
        {
            Rtl.WriteToStream(Output, LongnamesAmh.Bytes);
            foreach (String Longname in Longnames)
            {
                Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(Longname + '\0'));
            }
            if (LongnamesAmh.PadSize != 0)
            {
                Output.WriteByte(Archive.Pad);
            }
        }

        /* Import Headers */
        foreach (var Import in Imports)
        {
            if (Import.LibLongnameOffset != null)
            {
                Rtl.WriteToStream(Output, new ArchiveMemberHeader((UInt32)Import.LibLongnameOffset, (UInt32)Import.Data.Length).Bytes);
            } else
            {
                Rtl.WriteToStream(Output, new ArchiveMemberHeader(Import.LibName, (UInt32)Import.Data.Length).Bytes);
            }
            Rtl.WriteToStream(Output, Import.Data);
            if (Import.Data.Length % 2 != 0)
            {
                Output.WriteByte(Archive.Pad);
            }
        }

        Byte[] Bytes = Rtl.ResizeArray(Output.GetBuffer(), (Int32)Output.Position);
        Output.Dispose();
        return Bytes;
    }
}
