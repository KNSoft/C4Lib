using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

public class ObjectFile
{
    public class Section
    {
        public SectionHeader Header;
        public Byte[] Data;
        public Relocation[]? Relocations;

        public Section(String Name, Byte[] Data, Relocation[]? Relocations, IMAGE_SCN Characteristics)
        {
            Header = new(Name, 0, (UInt32)Data.Length, 0, (UInt16)(Relocations == null ? 0 : Relocations.Length), Characteristics);
            this.Data = Data;
            this.Relocations = Relocations;
        }
    }

    public FileHeader FileHeader;

    public readonly List<Section> Sections = new();

    public readonly List<Symbol> Symbols = new();

    public readonly List<KeyValuePair<UInt32, Byte[]>> StringTable = new(); // Offset -> String

    public readonly List<String> SymbolNames = new();

    public ObjectFile(IMAGE_FILE_MACHINE Machine, IMAGE_FILE_CHARACTERISTICS AdditionalFileCharacteristics)
    {
        FileHeader = new(Machine, AdditionalFileCharacteristics);
    }

    public UInt16 AddSection(Section Section)
    {
        Sections.Add(Section);

        /* Update offsets */

        UInt32 Offset = (UInt32)(Marshal.SizeOf<IMAGE_FILE_HEADER>() + Sections.Count * Marshal.SizeOf<IMAGE_SECTION_HEADER>());

        foreach (Section ExistingSection in Sections)
        {
            if (ExistingSection.Data.Length > 0)
            {
                ExistingSection.Header.NativeStruct.PointerToRawData = Offset;
                Offset += (UInt32)ExistingSection.Data.Length;
            }
            if (ExistingSection.Relocations != null)
            {
                ExistingSection.Header.NativeStruct.PointerToRelocations = Offset;
                Offset += (UInt32)(ExistingSection.Relocations.Length * Marshal.SizeOf<IMAGE_RELOCATION>());
            }
        }

        FileHeader.NativeStruct.PointerToSymbolTable = Offset;

        return FileHeader.NativeStruct.NumberOfSections++;
    }

    public void AddSymbol(String Name, UInt32 Value, Int16 SectionNumber, IMAGE_SYM_TYPE TypeMSB, IMAGE_SYM_DTYPE TypeLSB, IMAGE_SYM_CLASS StorageClass)
    {
        Byte[]? NameBytes = Symbol.GetNameBytes(Name);

        if (NameBytes == null)
        {
            UInt32 Offset = StringTable.Count == 0 ? sizeof(UInt32) : (StringTable.Last().Key + (UInt32)StringTable.Last().Value.Length + 1);
            StringTable.Add(new(Offset, Encoding.ASCII.GetBytes(Name)));
            NameBytes = Symbol.GetNameBytes(Offset);
        }

        Symbols.Add(new(NameBytes, Value, SectionNumber, TypeMSB, TypeLSB, StorageClass));

        if (StorageClass == IMAGE_SYM_CLASS.EXTERNAL &&
            SectionNumber > 0)
        {
            SymbolNames.Add(Name);
        }

        FileHeader.NativeStruct.NumberOfSymbols++;
    }

    public void Write(Stream Output)
    {
        /* Write file header (IMAGE_FILE_HEADER) */
        Rtl.StreamWrite(Output, Rtl.StructToRaw(FileHeader.NativeStruct));

        /* Write section headers (IMAGE_SECTION_HEADER) */
        foreach (Section Section in Sections)
        {
            Rtl.StreamWrite(Output, Rtl.StructToRaw(Section.Header.NativeStruct));
        }

        /* Write section data and relocations */
        foreach (Section Section in Sections)
        {
            if (Section.Data.Length > 0)
            {
                Rtl.StreamWrite(Output, Section.Data);
            }
            if (Section.Relocations != null)
            {
                foreach (Relocation Relocation in Section.Relocations)
                {
                    Rtl.StreamWrite(Output, Rtl.StructToRaw(Relocation.NativeStruct));
                }
            }
        }

        /* Write symbols (IMAGE_SYMBOL) */
        foreach (Symbol Symbol in Symbols)
        {
            Rtl.StreamWrite(Output, Rtl.StructToRaw(Symbol.NativeStruct));
        }

        /* Write string table */
        Rtl.StreamWrite(Output, BitConverter.GetBytes((UInt32)(sizeof(UInt32) + StringTable.Sum(x => x.Value.Length) + StringTable.Count)));
        foreach (var StringTableItem in StringTable)
        {
            Rtl.StreamWrite(Output, StringTableItem.Value);
            Output.WriteByte(0);
        }
    }

    private static readonly String NullIIDSymbolName = "__NULL_IMPORT_DESCRIPTOR";

    public static ObjectFile NewNullIIDObject(IMAGE_FILE_MACHINE Machine)
    {
        ObjectFile NewObject = new(Machine, IMAGE_FILE_CHARACTERISTICS.DEBUG_STRIPPED);

        UInt16 idata3Index = NewObject.AddSection(new(".idata$3",
                                                      new Byte[Marshal.SizeOf<IMAGE_IMPORT_DESCRIPTOR>()],
                                                      null,
                                                      (IMAGE_SCN)SectionHeader.SCN.idata | IMAGE_SCN.ALIGN_4BYTES));

        NewObject.AddSymbol(NullIIDSymbolName,
                            0,
                            (Int16)(idata3Index + 1),
                            IMAGE_SYM_TYPE.NULL,
                            IMAGE_SYM_DTYPE.NULL,
                            IMAGE_SYM_CLASS.EXTERNAL);

        return NewObject;
    }

    public static ObjectFile NewDllImportStubObject(IMAGE_FILE_MACHINE Machine, String DllName)
    {
        ObjectFile NewObject = new(Machine, IMAGE_FILE_CHARACTERISTICS.DEBUG_STRIPPED);

        UInt16 RelocType = Machine switch
        {
            IMAGE_FILE_MACHINE.I386 => (UInt16)IMAGE_REL.I386.DIR32NB,
            IMAGE_FILE_MACHINE.AMD64 => (UInt16)IMAGE_REL.AMD64.ADDR32NB,
            IMAGE_FILE_MACHINE.ARM64 => (UInt16)IMAGE_REL.ARM64.ADDR32NB,
            IMAGE_FILE_MACHINE.ARMNT => (UInt16)IMAGE_REL.ARM.ADDR32NB,
            _ => throw new NotImplementedException("Unsupported machine type: " + Machine.ToString())
        };

        /* Add sections */
        UInt16 idata2Index = NewObject.AddSection(new(".idata$2",
                                                      new Byte[Marshal.SizeOf<IMAGE_IMPORT_DESCRIPTOR>()],
                                                      new Relocation[]
                                                      {
                                                          new(0xC, 2, RelocType),
                                                          new(0x0, 3, RelocType),
                                                          new(0x10, 4, RelocType)
                                                      },
                                                      (IMAGE_SCN)SectionHeader.SCN.idata | IMAGE_SCN.ALIGN_4BYTES));
        UInt16 idata6Index = NewObject.AddSection(new(".idata$6",
                                                      Encoding.ASCII.GetBytes(DllName + (DllName.Length % 2 == 0 ? "\0\0" : "\0")),
                                                      null,
                                                      (IMAGE_SCN)SectionHeader.SCN.idata | IMAGE_SCN.ALIGN_2BYTES));
        UInt16 idata5Index = NewObject.AddSection(new(".idata$5",
                                                      new Byte[NewObject.FileHeader.SizeOfPointer],
                                                      null,
                                                      (IMAGE_SCN)SectionHeader.SCN.idata | IMAGE_SCN.ALIGN_4BYTES));
        UInt16 idata4Index = NewObject.AddSection(new(".idata$4",
                                                      new Byte[NewObject.FileHeader.SizeOfPointer],
                                                      null,
                                                      (IMAGE_SCN)SectionHeader.SCN.idata | IMAGE_SCN.ALIGN_4BYTES));

        /* Add symbols */
        String DllShortName = Path.HasExtension(DllName) ? Path.ChangeExtension(DllName, null) : DllName;

        NewObject.AddSymbol("__IMPORT_DESCRIPTOR_" + DllShortName, 0, (Int16)(idata2Index + 1), IMAGE_SYM_TYPE.NULL, IMAGE_SYM_DTYPE.NULL, IMAGE_SYM_CLASS.EXTERNAL);
        NewObject.AddSymbol(".idata$2", (UInt32)SectionHeader.SCN.idata, (Int16)(idata2Index + 1), IMAGE_SYM_TYPE.NULL, IMAGE_SYM_DTYPE.NULL, IMAGE_SYM_CLASS.SECTION);
        NewObject.AddSymbol(".idata$6", 0, (Int16)(idata6Index + 1), IMAGE_SYM_TYPE.NULL, IMAGE_SYM_DTYPE.NULL, IMAGE_SYM_CLASS.STATIC);
        NewObject.AddSymbol(".idata$4", (UInt32)SectionHeader.SCN.idata, (Int16)(idata4Index + 1), IMAGE_SYM_TYPE.NULL, IMAGE_SYM_DTYPE.NULL, IMAGE_SYM_CLASS.SECTION);
        NewObject.AddSymbol(".idata$5", (UInt32)SectionHeader.SCN.idata, (Int16)(idata5Index + 1), IMAGE_SYM_TYPE.NULL, IMAGE_SYM_DTYPE.NULL, IMAGE_SYM_CLASS.SECTION);
        NewObject.AddSymbol(NullIIDSymbolName, 0, 0, IMAGE_SYM_TYPE.NULL, IMAGE_SYM_DTYPE.NULL, IMAGE_SYM_CLASS.EXTERNAL);
        NewObject.AddSymbol('\x7F' + DllShortName + "_NULL_THUNK_DATA", 0, (Int16)(idata5Index + 1), IMAGE_SYM_TYPE.NULL, IMAGE_SYM_DTYPE.NULL, IMAGE_SYM_CLASS.EXTERNAL);

        return NewObject;
    }
}
