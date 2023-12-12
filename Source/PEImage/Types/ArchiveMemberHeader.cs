using System;
using System.IO;
using System.Linq;
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
    /* Well-known name constants */
    public static readonly Byte[] LinkerMemberName = "/               "u8.ToArray();
    public static readonly Byte[] LongNamesMemberName = "//              "u8.ToArray();

    /* Private constants */
    private static readonly Byte[] DummyDate = "-1          "u8.ToArray();
    private static readonly Byte[] DummyID = "      "u8.ToArray();
    private static readonly Byte[] DummyMode = "0       "u8.ToArray();

    /* Public properties */
    public IMAGE_ARCHIVE_MEMBER_HEADER NativeStruct;
    public UInt32 Size;

    /* Create new */
    public ArchiveMemberHeader(Byte[] NameBytes, UInt32 Size)
    {
        Byte[] FilledNameBytes = new Byte[16];

        NameBytes.CopyTo(FilledNameBytes, 0);
        for (Int32 i = NameBytes.Length; i < FilledNameBytes.Length; i++)
        {
            FilledNameBytes[i] = (Byte)' ';
        }

        this.Size = Size;
        String MemberSize = Size.ToString();
        NativeStruct = new IMAGE_ARCHIVE_MEMBER_HEADER()
        {
            Name = FilledNameBytes,
            Date = DummyDate,
            UserID = DummyID,
            GroupID = DummyID,
            Mode = DummyMode,
            Size = Encoding.ASCII.GetBytes(MemberSize + new String(' ', 10 - MemberSize.Length)),
            EndHeader = ArchiveFile.End
        };
    }

    /* Load existing */
    public ArchiveMemberHeader(Byte[] RawData)
    {
        NativeStruct = Rtl.RawToStruct<IMAGE_ARCHIVE_MEMBER_HEADER>(RawData);

        /* Verify data */
        if (!NativeStruct.EndHeader.SequenceEqual(ArchiveFile.End))
        {
            throw new InvalidDataException();
        }

        /* Fill members */
        Int32 i = ASCIIBytesToInt(NativeStruct.Size, 0);
        Size = i >= 0 ? (UInt32)i : throw new InvalidDataException();
    }

    public String? GetName(out UInt32? LongnameOffset)
    {
        Int32 i;

        if (NativeStruct.Name[0] == '/')
        {
            i = ASCIIBytesToInt(NativeStruct.Name, 1);
            LongnameOffset = i >= 0 ? (UInt32)i : throw new InvalidDataException();
            return null;
        } else
        {
            for (i = 0; i < NativeStruct.Name.Length; i++)
            {
                if (NativeStruct.Name[i] == '/')
                {
                    break;
                }
            }
            if (i < NativeStruct.Name.Length)
            {
                LongnameOffset = null;
                return Encoding.ASCII.GetString(NativeStruct.Name, 0, i);
            } else
            {
                throw new InvalidDataException();
            }
        }
    }

    public static Byte[] GetNameBytes(UInt32 NameOffset)
    {
        return Encoding.ASCII.GetBytes("/" + NameOffset.ToString());
    }

    public static Byte[]? GetNameBytes(String Name)
    {
        Byte[] NameBytes = Encoding.ASCII.GetBytes(Name + "/");

        if (NameBytes.Length > 16)
        {
            return null;
        }

        return NameBytes;
    }

    private static Int32 ASCIIBytesToInt(Byte[] Bytes, Int32 StartIndex)
    {
        Int32 i;

        for (i = StartIndex; i < Bytes.Length; i++)
        {
            if (Bytes[i] == (Byte)' ')
            {
                break;
            } else if (!Char.IsDigit((Char)Bytes[i]))
            {
                throw new InvalidDataException();
            }
        }
        if (i == 0)
        {
            throw new InvalidDataException();
        }

        return Int32.Parse(Encoding.ASCII.GetString(Bytes, StartIndex, i));
    }
}
