using System.Buffers.Binary;

namespace NdsRom.NRom.NintendoDs;

public sealed record NdsValidationIssue(bool IsError, string Message);

public static class NdsRomValidator
{
    public static IReadOnlyList<NdsValidationIssue> ValidateFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Validate(fs);
    }

    public static IReadOnlyList<NdsValidationIssue> Validate(Stream stream)
    {
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("O stream precisa permitir leitura e seek.", nameof(stream));

        var issues = new List<NdsValidationIssue>();
        var oldPosition = stream.Position;
        try
        {
            if (stream.Length < 0x200)
            {
                issues.Add(new(true, "ROM menor que 0x200 bytes."));
                return issues;
            }

            stream.Position = 0;
            var headerBytes = ReadExactly(stream, 0x200);
            var header = NdsHeader.Parse(headerBytes);

            if (header.UnitCode != 0)
                issues.Add(new(true, $"UnitCode {header.UnitCode} não é NTR puro; esta implementação suporta ROMs Nintendo DS (UnitCode 0)."));

            var expectedLogo = NdsCrc16.Compute(headerBytes.AsSpan(NdsHeader.NintendoLogoOffset, NdsHeader.NintendoLogoLength));
            if (header.NintendoLogoCrc != expectedLogo)
                issues.Add(new(true, $"Nintendo Logo CRC inválido: header=0x{header.NintendoLogoCrc:X4}, calculado=0x{expectedLogo:X4}."));

            var expectedHeader = NdsCrc16.Compute(headerBytes.AsSpan(0, NdsHeader.HeaderChecksumEndExclusive));
            if (header.HeaderCrc != expectedHeader)
                issues.Add(new(true, $"Header CRC inválido: header=0x{header.HeaderCrc:X4}, calculado=0x{expectedHeader:X4}."));

            CheckRange(issues, "ARM9", header.Arm9Offset, header.Arm9Size, stream.Length);
            CheckRange(issues, "ARM7", header.Arm7Offset, header.Arm7Size, stream.Length);
            CheckRange(issues, "FNT", header.FntOffset, header.FntSize, stream.Length);
            CheckRange(issues, "FAT", header.FatOffset, header.FatSize, stream.Length);
            if (header.Arm9OverlaySize != 0)
                CheckRange(issues, "ARM9 overlay table", header.Arm9OverlayOffset, header.Arm9OverlaySize, stream.Length);
            if (header.Arm7OverlaySize != 0)
                CheckRange(issues, "ARM7 overlay table", header.Arm7OverlayOffset, header.Arm7OverlaySize, stream.Length);

            if (header.FatSize % 8 != 0)
                issues.Add(new(true, $"FAT size 0x{header.FatSize:X} não é múltiplo de 8."));

            if (header.Arm9Offset >= 0x4000 && header.Arm9Offset < 0x8000)
            {
                var length = checked((int)(0x8000 - header.Arm9Offset));
                if ((long)header.Arm9Offset + length <= stream.Length)
                {
                    stream.Position = header.Arm9Offset;
                    var secureArea = ReadExactly(stream, length);
                    var expectedSecure = NdsCrc16.Compute(secureArea);
                    if (header.SecureAreaCrc != expectedSecure)
                        issues.Add(new(true, $"Secure Area CRC inválido: header=0x{header.SecureAreaCrc:X4}, calculado=0x{expectedSecure:X4}."));
                }
            }

            if (header.UsedRomSize > stream.Length)
                issues.Add(new(true, $"Used ROM Size 0x{header.UsedRomSize:X} é maior que o arquivo (0x{stream.Length:X})."));

            if (header.FatSize >= 8 && (long)header.FatOffset + header.FatSize <= stream.Length)
            {
                stream.Position = header.FatOffset;
                using var br = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
                var count = (int)(header.FatSize / 8);
                for (var i = 0; i < count; i++)
                {
                    var begin = br.ReadUInt32();
                    var end = br.ReadUInt32();
                    if (end < begin || end > stream.Length)
                    {
                        issues.Add(new(true, $"FAT[{i}] inválida: 0x{begin:X8}-0x{end:X8}."));
                        break;
                    }
                }
            }

            if (issues.Count == 0)
                issues.Add(new(false, "Header, Secure Area CRC, Nintendo Logo CRC, FAT e limites básicos estão válidos."));

            return issues;
        }
        finally
        {
            stream.Position = oldPosition;
        }
    }

    private static void CheckRange(List<NdsValidationIssue> issues, string name, uint offset, uint size, long streamLength)
    {
        if (size == 0)
            return;

        var end = (ulong)offset + size;
        if (offset < 0x200 || end > (ulong)streamLength)
            issues.Add(new(true, $"{name} fora da ROM: offset=0x{offset:X}, size=0x{size:X}."));
    }

    private static byte[] ReadExactly(Stream stream, int count)
    {
        var buffer = new byte[count];
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(buffer, total, count - total);
            if (read == 0)
                throw new EndOfStreamException();
            total += read;
        }
        return buffer;
    }
}
