using System;

namespace KNSoft.C4Lib.PEImage;

public class ArchiveFile
{
    public static readonly Byte[] Start = "!<arch>\n"u8.ToArray();
    public static readonly Byte[] End = { 0x60, 0x0A };
    public static readonly Byte Pad = 0x0A;

    public IMAGE_FILE_MACHINE Machine;

    public ArchiveFile(IMAGE_FILE_MACHINE Machine)
    {
        this.Machine = Machine;
    }
}
