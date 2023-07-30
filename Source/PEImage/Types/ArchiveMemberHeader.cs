using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KNSoft.C4Lib.PEImage;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IMAGE_ARCHIVE_MEMBER_HEADER
{
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 16)]
    public Byte[] Name;
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 12)]
    public Byte[] Date;
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 6)]
    public Byte[] UserID;
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 6)]
    public Byte[] GroupID;
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 8)]
    public Byte[] Mode;
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 10)]
    public Byte[] Size;
    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U8, SizeConst = 2)]
    public Byte[] EndHeader;
}

public class ArchiveMemberHeader
{
    public static readonly Int32 MaxShortNameLength = 15; // One byte for postfix '/'
    public static readonly String LinkerMemberName = "/";
    public static readonly String LongNamesMemberName = "//";
    public static readonly String HybridMapMemberName = "/<HYBRIDMAP>/";
    public static readonly String LongNameMemberNamePrefix = "/";
    public static readonly String MemberNamePostfix = "/";

    private static readonly Byte[] DummyDate = "-1          "u8.ToArray();
    private static readonly Byte[] DummyID = "      "u8.ToArray();
    private static readonly Byte[] DummyMode = "0       "u8.ToArray();

    public Byte[] Bytes;
    public UInt32 Size;

    public ArchiveMemberHeader(String Name, UInt32 Size)
    {
        String LibName = Name.Length > MaxShortNameLength ? Name[..MaxShortNameLength] : Name;
        String MemberSize = Size.ToString();
        Bytes = Rtl.StructToRaw(new IMAGE_ARCHIVE_MEMBER_HEADER()
        {
            Name = Encoding.ASCII.GetBytes(LibName + new String(' ', 16 - LibName.Length)),
            Date = DummyDate,
            UserID = DummyID,
            GroupID = DummyID,
            Mode = DummyMode,
            Size = Encoding.ASCII.GetBytes(MemberSize + new String(' ', 10 - MemberSize.Length)),
            EndHeader = ArchiveFile.End
        });
        this.Size = Size;
    }
}
