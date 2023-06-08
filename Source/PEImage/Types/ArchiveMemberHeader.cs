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
    private static readonly Byte[] DummyDate = "-1          "u8.ToArray();
    private static readonly Byte[] DummyID = "      "u8.ToArray();
    private static readonly Byte[] DummyMode = "0       "u8.ToArray();
    public static readonly Int32 MaxShortNameLength = 15;

    public Byte[] Bytes;
    public Byte PadSize;

    private void Init(String LibName, UInt32 Size)
    {
        String MemberSize = Size.ToString();
        Bytes = Rtl.StructToRaw(new IMAGE_ARCHIVE_MEMBER_HEADER()
        {
            Name = Encoding.ASCII.GetBytes(LibName + new String(' ', 16 - LibName.Length)),
            Date = DummyDate,
            UserID = DummyID,
            GroupID = DummyID,
            Mode = DummyMode,
            Size = Encoding.ASCII.GetBytes(MemberSize + new String(' ', 10 - MemberSize.Length)),
            EndHeader = Archive.End
        });
        PadSize = (Byte)(Size % 2 == 0 ? 0 : 1);
    }

#pragma warning disable CS8618
    public ArchiveMemberHeader(String Name, UInt32 Size)
    {
        Init((Name.Length > MaxShortNameLength ? Name[..MaxShortNameLength] : Name) + '/', Size);
    }

    public ArchiveMemberHeader(UInt32 LongNameOffset, UInt32 Size)
    {
        Init('/' + LongNameOffset.ToString(), Size);
    }
#pragma warning restore CS8618
}
