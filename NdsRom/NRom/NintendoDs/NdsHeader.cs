using System.Buffers.Binary;
using System.Text;

namespace NdsRom.NRom.NintendoDs;

/// <summary>
/// NTR/Nintendo DS cartridge header fields needed by the extractor/rebuilder.
/// Unknown/reserved bytes are not regenerated: NdsRomImage preserves the
/// original header area and patches only these documented fields.
/// </summary>
public sealed class NdsHeader
{
    public const int HeaderChecksumEndExclusive = 0x15E;
    public const int NintendoLogoOffset = 0x0C0;
    public const int NintendoLogoLength = 0x09C;

    public string GameTitle { get; private set; } = string.Empty;
    public string GameCode { get; private set; } = string.Empty;
    public byte UnitCode { get; private set; }
    public byte DeviceCapacity { get; set; }

    public uint Arm9Offset { get; set; }
    public uint Arm9EntryAddress { get; private set; }
    public uint Arm9LoadAddress { get; private set; }
    public uint Arm9Size { get; set; }

    public uint Arm7Offset { get; set; }
    public uint Arm7EntryAddress { get; private set; }
    public uint Arm7LoadAddress { get; private set; }
    public uint Arm7Size { get; set; }

    public uint FntOffset { get; set; }
    public uint FntSize { get; set; }
    public uint FatOffset { get; set; }
    public uint FatSize { get; set; }

    public uint Arm9OverlayOffset { get; set; }
    public uint Arm9OverlaySize { get; set; }
    public uint Arm7OverlayOffset { get; set; }
    public uint Arm7OverlaySize { get; set; }

    public uint BannerOffset { get; set; }
    public ushort SecureAreaCrc { get; set; }
    public uint UsedRomSize { get; set; }
    public uint HeaderSize { get; private set; }
    public ushort NintendoLogoCrc { get; set; }
    public ushort HeaderCrc { get; set; }

    internal static NdsHeader Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 0x200)
            throw new InvalidDataException("Arquivo pequeno demais para conter um header NDS.");

        var header = new NdsHeader
        {
            GameTitle = ReadAscii(bytes.Slice(0x000, 12)),
            GameCode = ReadAscii(bytes.Slice(0x00C, 4)),
            UnitCode = bytes[0x012],
            DeviceCapacity = bytes[0x014],

            Arm9Offset = U32(bytes, 0x020),
            Arm9EntryAddress = U32(bytes, 0x024),
            Arm9LoadAddress = U32(bytes, 0x028),
            Arm9Size = U32(bytes, 0x02C),

            Arm7Offset = U32(bytes, 0x030),
            Arm7EntryAddress = U32(bytes, 0x034),
            Arm7LoadAddress = U32(bytes, 0x038),
            Arm7Size = U32(bytes, 0x03C),

            FntOffset = U32(bytes, 0x040),
            FntSize = U32(bytes, 0x044),
            FatOffset = U32(bytes, 0x048),
            FatSize = U32(bytes, 0x04C),

            Arm9OverlayOffset = U32(bytes, 0x050),
            Arm9OverlaySize = U32(bytes, 0x054),
            Arm7OverlayOffset = U32(bytes, 0x058),
            Arm7OverlaySize = U32(bytes, 0x05C),

            BannerOffset = U32(bytes, 0x068),
            SecureAreaCrc = U16(bytes, 0x06C),
            UsedRomSize = U32(bytes, 0x080),
            HeaderSize = U32(bytes, 0x084),
            NintendoLogoCrc = U16(bytes, 0x15C),
            HeaderCrc = U16(bytes, 0x15E)
        };

        return header;
    }

    internal void ApplyTo(Span<byte> bytes)
    {
        if (bytes.Length < 0x200)
            throw new ArgumentException("Header buffer must be at least 0x200 bytes.", nameof(bytes));

        bytes[0x014] = DeviceCapacity;

        W32(bytes, 0x020, Arm9Offset);
        W32(bytes, 0x02C, Arm9Size);
        W32(bytes, 0x030, Arm7Offset);
        W32(bytes, 0x03C, Arm7Size);
        W32(bytes, 0x040, FntOffset);
        W32(bytes, 0x044, FntSize);
        W32(bytes, 0x048, FatOffset);
        W32(bytes, 0x04C, FatSize);
        W32(bytes, 0x050, Arm9OverlayOffset);
        W32(bytes, 0x054, Arm9OverlaySize);
        W32(bytes, 0x058, Arm7OverlayOffset);
        W32(bytes, 0x05C, Arm7OverlaySize);
        W32(bytes, 0x068, BannerOffset);
        W16(bytes, 0x06C, SecureAreaCrc);
        W32(bytes, 0x080, UsedRomSize);
        W16(bytes, 0x15C, NintendoLogoCrc);
        W16(bytes, 0x15E, HeaderCrc);
    }

    internal static ushort U16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));

    internal static uint U32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));

    internal static void W16(Span<byte> bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(offset, 2), value);

    internal static void W32(Span<byte> bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset, 4), value);

    private static string ReadAscii(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        if (end < 0)
            end = bytes.Length;
        return Encoding.ASCII.GetString(bytes[..end]).TrimEnd();
    }
}
