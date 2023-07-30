using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

public class ObjectFile
{
    private struct Section
    {
        public String Name;
        public IMAGE_SCN Align;
        public Byte[] Data;
        public Relocation[]? Reloc;

        public Section(String Name, IMAGE_SCN Align, Byte[] Data, Relocation[]? Reloc)
        {
            this.Name = Name;
            this.Align = Align;
            this.Data = Data;
            this.Reloc = Reloc;
        }
    }

    private static readonly String NullIIDSymbolName = "__NULL_IMPORT_DESCRIPTOR";

    public static String WriteNullIIDObjectFile(Stream Output, IMAGE_FILE_MACHINE Machine)
    {
        /* Something depends on machine type */
        IMAGE_FILE_CHARACTERISTICS FileCharacteristics = IMAGE_FILE_CHARACTERISTICS.DEBUG_STRIPPED;
        if (FileHeader.GetMachineBits(Machine) == 32)
        {
            FileCharacteristics |= IMAGE_FILE_CHARACTERISTICS._32BIT_MACHINE;
        }

        /* Section and symbol*/
        Section Section = new(".idata$3", IMAGE_SCN.ALIGN_4BYTES, new Byte[Marshal.SizeOf<IMAGE_IMPORT_DESCRIPTOR>()], null);
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
        Rtl.WriteToStream(Output, BitConverter.GetBytes(sizeof(UInt32) + (UInt32)NullIIDSymbolName.Length + 1));
        Rtl.WriteToStream(Output, Encoding.ASCII.GetBytes(NullIIDSymbolName));
        Output.WriteByte(0);

        return NullIIDSymbolName;
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

    public static String[] WriteDllImportStubObjectFile(Stream Output, IMAGE_FILE_MACHINE Machine, String DllName)
    {
        UInt32 MachineBits;
        UInt32 Offset;
        UInt16 RelocType;
        UInt32 SizeOfPointer;

        /* Something depends on machine type */
        IMAGE_FILE_CHARACTERISTICS FileCharacteristics = IMAGE_FILE_CHARACTERISTICS.DEBUG_STRIPPED;
        MachineBits = FileHeader.GetMachineBits(Machine);
        if (MachineBits == 32)
        {
            FileCharacteristics |= IMAGE_FILE_CHARACTERISTICS._32BIT_MACHINE;
        }
        SizeOfPointer = MachineBits / 8;
        RelocType = Machine switch
        {
            IMAGE_FILE_MACHINE.I386 => (UInt16)IMAGE_REL.I386.DIR32NB,
            IMAGE_FILE_MACHINE.AMD64 => (UInt16)IMAGE_REL.AMD64.ADDR32NB,
            IMAGE_FILE_MACHINE.ARM64 => (UInt16)IMAGE_REL.ARM64.ADDR32NB,
            IMAGE_FILE_MACHINE.ARM => (UInt16)IMAGE_REL.ARM.ADDR32NB,
            _ => throw new NotImplementedException("Unsupported machine type: " + Machine.ToString())
        };

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
        Section[] Sections =
        {
            /* 1 */ new Section(".idata$2", IMAGE_SCN.ALIGN_4BYTES, new Byte[Marshal.SizeOf<IMAGE_IMPORT_DESCRIPTOR>()], idata2Relocs),
            /* 2 */ new Section(".idata$6", IMAGE_SCN.ALIGN_2BYTES, Encoding.ASCII.GetBytes(DllName + (DllName.Length % 2 == 0 ? "\0\0" : "\0")), null),
            /* 3 */ new Section(".idata$5", IMAGE_SCN.ALIGN_4BYTES, new Byte[SizeOfPointer], null),
            /* 4 */ new Section(".idata$4", IMAGE_SCN.ALIGN_4BYTES, new Byte[SizeOfPointer], null)
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

        return SymbolNames;
    }
}
