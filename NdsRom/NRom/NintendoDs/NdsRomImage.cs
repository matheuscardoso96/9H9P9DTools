using System.Buffers.Binary;

namespace NdsRom.NRom.NintendoDs;

/// <summary>
/// Dependency-free Nintendo DS (NTR) ROM extractor/rebuilder for the translation workflow.
///
/// Design choices for compatibility:
/// - keeps the original pre-ARM9/header area byte-for-byte except documented header fields;
/// - keeps the original FNT byte-for-byte (important for Shift-JIS names);
/// - keeps overlay tables byte-for-byte and preserves original FAT file IDs;
/// - rebuilds FAT offsets after file replacement;
/// - recalculates Secure Area CRC, Nintendo Logo CRC and Header CRC;
/// - refuses to save if the encrypted first 0x800 bytes of ARM9 were changed.
///
/// It intentionally does not add/delete/rename NitroFS files. Replacing file contents is supported.
/// </summary>
public sealed class NdsRomImage
{
    private const uint Arm9FooterMagic = 0xDEC00621;
    private const int Alignment = 0x200;
    private const int EncryptedSecureAreaLength = 0x800;

    private readonly byte[] _originalPrefix;
    private readonly byte[] _originalEncryptedArm9Prefix;
    private readonly byte[]? _arm9Footer;
    private readonly byte[] _arm9OverlayTable;
    private readonly byte[] _arm7OverlayTable;
    private readonly byte[] _fntRaw;
    private readonly long _originalRomLength;
    private readonly List<NdsRomFile> _files;
    private readonly NdsRomFile[] _fatFiles;
    private readonly NdsRomFile _arm9;
    private readonly NdsRomFile _arm7;
    private readonly NdsRomFile? _banner;

    private NdsRomImage(
        NdsHeader header,
        byte[] originalPrefix,
        byte[] originalEncryptedArm9Prefix,
        byte[]? arm9Footer,
        byte[] arm9OverlayTable,
        byte[] arm7OverlayTable,
        byte[] fntRaw,
        long originalRomLength,
        List<NdsRomFile> files,
        NdsRomFile[] fatFiles,
        NdsRomFile arm9,
        NdsRomFile arm7,
        NdsRomFile? banner)
    {
        Header = header;
        _originalPrefix = originalPrefix;
        _originalEncryptedArm9Prefix = originalEncryptedArm9Prefix;
        _arm9Footer = arm9Footer;
        _arm9OverlayTable = arm9OverlayTable;
        _arm7OverlayTable = arm7OverlayTable;
        _fntRaw = fntRaw;
        _originalRomLength = originalRomLength;
        _files = files;
        _fatFiles = fatFiles;
        _arm9 = arm9;
        _arm7 = arm7;
        _banner = banner;
    }

    public NdsHeader Header { get; }
    public IReadOnlyList<NdsRomFile> Files => _files;

    public static NdsRomImage Load(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Load(fs);
    }

    public static NdsRomImage Load(Stream input)
    {
        if (!input.CanRead || !input.CanSeek)
            throw new ArgumentException("O stream da ROM precisa permitir leitura e seek.", nameof(input));

        if (input.Length > int.MaxValue)
            throw new NotSupportedException("ROMs maiores que 2 GiB não são suportadas.");

        input.Position = 0;
        var rom = ReadExactly(input, checked((int)input.Length));
        if (rom.Length < 0x4000)
            throw new InvalidDataException("ROM NDS pequena demais.");

        var header = NdsHeader.Parse(rom);
        if (header.UnitCode != 0)
            throw new NotSupportedException($"Esta implementação foi feita para ROMs NTR/Nintendo DS (UnitCode 0). UnitCode encontrado: {header.UnitCode}.");

        ValidateHeaderRegions(header, rom.LongLength);

        var prefixLength = checked((int)header.Arm9Offset);
        if (prefixLength < 0x200 || prefixLength > rom.Length)
            throw new InvalidDataException($"ARM9 offset inválido: 0x{header.Arm9Offset:X}.");

        var prefix = Slice(rom, 0, prefixLength);
        var arm9Bytes = Slice(rom, header.Arm9Offset, header.Arm9Size);
        var arm7Bytes = Slice(rom, header.Arm7Offset, header.Arm7Size);
        var arm9Ovt = header.Arm9OverlaySize == 0
            ? Array.Empty<byte>()
            : Slice(rom, header.Arm9OverlayOffset, header.Arm9OverlaySize);
        var arm7Ovt = header.Arm7OverlaySize == 0
            ? Array.Empty<byte>()
            : Slice(rom, header.Arm7OverlayOffset, header.Arm7OverlaySize);
        var fnt = Slice(rom, header.FntOffset, header.FntSize);

        if (header.FatSize % 8 != 0)
            throw new InvalidDataException($"FAT size 0x{header.FatSize:X} não é múltiplo de 8.");

        var fatCount = checked((int)(header.FatSize / 8));
        var fatEntries = ReadFat(rom, header.FatOffset, fatCount);
        var fntPaths = NdsFileNameTable.Parse(fnt, fatCount);
        var overlayPaths = BuildOverlayPathMap(arm9Ovt, arm7Ovt, fatCount);

        byte[]? footer = null;
        var footerOffset = (ulong)header.Arm9Offset + header.Arm9Size;
        if (footerOffset + 12 <= (ulong)rom.Length &&
            BinaryPrimitives.ReadUInt32LittleEndian(rom.AsSpan((int)footerOffset, 4)) == Arm9FooterMagic)
        {
            footer = Slice(rom, (uint)footerOffset, 12);
        }

        NdsRomFile? banner = null;
        if (header.BannerOffset != 0)
        {
            var bannerSize = GetBannerSize(rom, header.BannerOffset);
            banner = new NdsRomFile("sys/banner.bin", NdsRomFileKind.Banner, Slice(rom, header.BannerOffset, (uint)bannerSize));
        }

        var arm9 = new NdsRomFile("sys/arm9.bin", NdsRomFileKind.Arm9, arm9Bytes);
        var arm7 = new NdsRomFile("sys/arm7.bin", NdsRomFileKind.Arm7, arm7Bytes);
        var files = new List<NdsRomFile> { arm9, arm7 };
        if (banner != null)
            files.Add(banner);

        var fatFiles = new NdsRomFile[fatCount];
        for (var fileId = 0; fileId < fatCount; fileId++)
        {
            var entry = fatEntries[fileId];
            var data = Slice(rom, entry.Begin, entry.End - entry.Begin);

            string path;
            NdsRomFileKind kind;
            if (overlayPaths.TryGetValue(fileId, out var overlay))
            {
                path = overlay.Path;
                kind = overlay.Kind;
            }
            else if (fntPaths.TryGetValue(fileId, out var nitroPath))
            {
                path = nitroPath;
                kind = NdsRomFileKind.NitroFs;
            }
            else
            {
                path = $"sys/fat/file_{fileId:0000}.bin";
                kind = NdsRomFileKind.UnknownFatEntry;
            }

            var file = new NdsRomFile(path, kind, data, fileId);
            fatFiles[fileId] = file;
            files.Add(file);
        }

        var encryptedPrefixLength = Math.Min(EncryptedSecureAreaLength, arm9Bytes.Length);
        var encryptedArm9Prefix = arm9Bytes.AsSpan(0, encryptedPrefixLength).ToArray();

        return new NdsRomImage(
            header,
            prefix,
            encryptedArm9Prefix,
            footer,
            arm9Ovt,
            arm7Ovt,
            fnt,
            rom.LongLength,
            files,
            fatFiles,
            arm9,
            arm7,
            banner);
    }

    public NdsRomFile? FindFile(string virtualPath)
    {
        var normalized = NdsRomFile.Normalize(virtualPath);
        return _files.FirstOrDefault(x => x.VirtualPath.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public void ReplaceFile(string virtualPath, byte[] data)
    {
        var file = FindFile(virtualPath)
            ?? throw new FileNotFoundException($"Arquivo não existe na ROM: {virtualPath}");
        file.Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public void ExtractToDirectory(string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in _files)
        {
            var diskPath = ToDiskPath(destination, file.VirtualPath);
            var directory = Path.GetDirectoryName(diskPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(diskPath, file.Data);
        }
    }

    /// <summary>
    /// Replaces every exported file that exists below sourceDirectory.
    /// Files missing from the directory keep their original ROM data.
    /// </summary>
    public int ApplyDirectory(string sourceDirectory)
    {
        var replaced = 0;
        foreach (var file in _files)
        {
            var diskPath = ToDiskPath(sourceDirectory, file.VirtualPath);
            if (!File.Exists(diskPath))
                continue;

            file.Data = File.ReadAllBytes(diskPath);
            replaced++;
        }
        return replaced;
    }

    public void Save(string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            Save(output);

        var validation = NdsRomValidator.ValidateFile(outputPath);
        var errors = validation.Where(x => x.IsError).ToArray();
        if (errors.Length > 0)
            throw new InvalidDataException("A ROM reconstruída falhou na validação:\n" + string.Join("\n", errors.Select(x => "- " + x.Message)));
    }

    public void Save(Stream output)
    {
        if (!output.CanWrite || !output.CanSeek)
            throw new ArgumentException("O stream de saída precisa permitir escrita e seek.", nameof(output));

        ValidateArm9SecurePrefix();

        output.Position = 0;
        output.SetLength(0);
        output.Write(_originalPrefix);

        // Keep ARM9 at its original ROM offset. This preserves the cartridge secure-area layout.
        var arm9Offset = Header.Arm9Offset;
        EnsurePosition(output, arm9Offset, 0xFF);
        output.Write(_arm9.Data);
        if (_arm9Footer != null)
            output.Write(_arm9Footer);
        Pad(output, Alignment, 0xFF);

        uint arm9OverlayOffset = 0;
        if (_arm9OverlayTable.Length != 0)
        {
            arm9OverlayOffset = CheckedU32(output.Position);
            output.Write(_arm9OverlayTable);
            Pad(output, Alignment, 0xFF);
        }

        var arm7Offset = CheckedU32(output.Position);
        output.Write(_arm7.Data);

        uint arm7OverlayOffset = 0;
        if (_arm7OverlayTable.Length != 0)
        {
            arm7OverlayOffset = CheckedU32(output.Position);
            output.Write(_arm7OverlayTable);
        }
        Pad(output, Alignment, 0xFF);

        // Preserve the original FNT bytes. This is deliberate: Shift-JIS names and directory/file IDs
        // remain exactly as they were in the original cartridge.
        var fntOffset = CheckedU32(output.Position);
        output.Write(_fntRaw);
        var fntSize = checked((uint)_fntRaw.Length);
        Pad(output, Alignment, 0xFF);

        // Reserve FAT. Entries are written after all file positions are known.
        var fatOffset = CheckedU32(output.Position);
        var fatSize = checked((uint)(_fatFiles.Length * 8));
        WriteFill(output, checked((int)fatSize), 0x00);
        Pad(output, Alignment, 0xFF);

        uint bannerOffset = 0;
        if (_banner != null)
        {
            bannerOffset = CheckedU32(output.Position);
            FixBannerCrcs(_banner.Data);
            output.Write(_banner.Data);
            Pad(output, Alignment, 0xFF);
        }

        var rebuiltFat = new FatEntry[_fatFiles.Length];
        ulong usedEnd = (ulong)output.Position;

        for (var fileId = 0; fileId < _fatFiles.Length; fileId++)
        {
            Pad(output, Alignment, 0xFF);
            var begin = CheckedU32(output.Position);
            var data = _fatFiles[fileId].Data;
            output.Write(data);
            var end = CheckedU32(output.Position);
            rebuiltFat[fileId] = new FatEntry(begin, end);
            usedEnd = Math.Max(usedEnd, (ulong)end);
        }

        // Write FAT using the original file IDs.
        var endPosition = output.Position;
        output.Position = fatOffset;
        using (var bw = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            foreach (var entry in rebuiltFat)
            {
                bw.Write(entry.Begin);
                bw.Write(entry.End);
            }
        }
        output.Position = endPosition;

        Header.Arm9Offset = arm9Offset;
        Header.Arm9Size = checked((uint)_arm9.Data.Length);
        Header.Arm9OverlayOffset = _arm9OverlayTable.Length == 0 ? 0 : arm9OverlayOffset;
        Header.Arm9OverlaySize = checked((uint)_arm9OverlayTable.Length);
        Header.Arm7Offset = arm7Offset;
        Header.Arm7Size = checked((uint)_arm7.Data.Length);
        Header.Arm7OverlayOffset = _arm7OverlayTable.Length == 0 ? 0 : arm7OverlayOffset;
        Header.Arm7OverlaySize = checked((uint)_arm7OverlayTable.Length);
        Header.FntOffset = fntOffset;
        Header.FntSize = fntSize;
        Header.FatOffset = fatOffset;
        Header.FatSize = fatSize;
        Header.BannerOffset = _banner == null ? 0 : bannerOffset;
        Header.UsedRomSize = CheckedU32((long)usedEnd);
        Header.DeviceCapacity = CalculateDeviceCapacity(Header.DeviceCapacity, usedEnd);

        // CRC of the raw cartridge secure area. The first 0x800 bytes remain encrypted and unchanged;
        // our splash hook at ARM9+0x800 is outside that KEY1-encrypted part but still inside this CRC area.
        Header.SecureAreaCrc = CalculateSecureAreaCrc(_arm9.Data, Header.Arm9Offset);

        var updatedPrefix = (byte[])_originalPrefix.Clone();
        Header.HeaderCrc = 0;
        Header.NintendoLogoCrc = 0;
        Header.ApplyTo(updatedPrefix);

        Header.NintendoLogoCrc = NdsCrc16.Compute(updatedPrefix.AsSpan(NdsHeader.NintendoLogoOffset, NdsHeader.NintendoLogoLength));
        Header.ApplyTo(updatedPrefix);

        Header.HeaderCrc = NdsCrc16.Compute(updatedPrefix.AsSpan(0, NdsHeader.HeaderChecksumEndExclusive));
        Header.ApplyTo(updatedPrefix);

        output.Position = 0;
        output.Write(updatedPrefix);

        // Preserve the original physical ROM length when possible. If the rebuilt data grew beyond it,
        // keep the ROM trimmed at the new used size; DeviceCapacity still advertises a sufficient cart size.
        var finalLength = Math.Max(_originalRomLength, (long)usedEnd);
        if (output.Length < finalLength)
        {
            output.Position = output.Length;
            WriteFill(output, checked((int)(finalLength - output.Length)), 0xFF);
        }
        else
        {
            output.SetLength(finalLength);
        }

        output.Flush();
    }

    private void ValidateArm9SecurePrefix()
    {
        if (Header.Arm9Offset < 0x4000 || Header.Arm9Offset >= 0x8000)
            return;

        if (_arm9.Data.Length < _originalEncryptedArm9Prefix.Length)
            throw new InvalidDataException("ARM9 modificado é menor que a região segura criptografada original.");

        if (!_arm9.Data.AsSpan(0, _originalEncryptedArm9Prefix.Length).SequenceEqual(_originalEncryptedArm9Prefix))
        {
            throw new InvalidDataException(
                "Os primeiros 0x800 bytes do ARM9 foram alterados. Essa área é KEY1-encrypted em ROMs comerciais. " +
                "A ferramenta não recriptografa KEY1 e, por segurança, recusou gerar uma ROM que poderia falhar no DS real. " +
                "O patch de splash deve começar em ARM9+0x800 ou depois.");
        }
    }

    private static ushort CalculateSecureAreaCrc(byte[] arm9, uint arm9Offset)
    {
        if (arm9Offset < 0x4000 || arm9Offset >= 0x8000)
            return 0;

        var length = checked((int)(0x8000 - arm9Offset));
        if (arm9.Length < length)
            throw new InvalidDataException($"ARM9 não contém a Secure Area completa ({length} bytes necessários).");

        return NdsCrc16.Compute(arm9.AsSpan(0, length));
    }

    private static void FixBannerCrcs(byte[] banner)
    {
        if (banner.Length < 0x20)
            return;

        var version = BinaryPrimitives.ReadUInt16LittleEndian(banner.AsSpan(0, 2));

        // Version 1 common area.
        if (banner.Length >= 0x840)
        {
            var crc = NdsCrc16.Compute(banner.AsSpan(0x20, 0x840 - 0x20));
            BinaryPrimitives.WriteUInt16LittleEndian(banner.AsSpan(0x02, 2), crc);
        }

        if (version >= 2 && banner.Length >= 0x940)
        {
            var crc = NdsCrc16.Compute(banner.AsSpan(0x20, 0x940 - 0x20));
            BinaryPrimitives.WriteUInt16LittleEndian(banner.AsSpan(0x04, 2), crc);
        }

        if (version >= 3 && banner.Length >= 0xA40)
        {
            var crc = NdsCrc16.Compute(banner.AsSpan(0x20, 0xA40 - 0x20));
            BinaryPrimitives.WriteUInt16LittleEndian(banner.AsSpan(0x06, 2), crc);
        }
    }

    private static int GetBannerSize(byte[] rom, uint offset)
    {
        RequireRange(rom.LongLength, offset, 2, "banner header");
        var version = BinaryPrimitives.ReadUInt16LittleEndian(rom.AsSpan((int)offset, 2));
        return version switch
        {
            1 => 0x840,
            2 => 0x940,
            3 => 0xA40,
            0x103 => throw new NotSupportedException("Banner DSi 0x103 não é suportado nesta implementação NTR."),
            _ => throw new InvalidDataException($"Versão de banner NDS desconhecida: 0x{version:X4}.")
        };
    }

    private static Dictionary<int, OverlayPath> BuildOverlayPathMap(byte[] arm9Ovt, byte[] arm7Ovt, int fatCount)
    {
        var result = new Dictionary<int, OverlayPath>();
        ParseOverlayTable(arm9Ovt, "overlay9", NdsRomFileKind.Arm9Overlay, fatCount, result);
        ParseOverlayTable(arm7Ovt, "overlay7", NdsRomFileKind.Arm7Overlay, fatCount, result);
        return result;
    }

    private static void ParseOverlayTable(
        byte[] table,
        string prefix,
        NdsRomFileKind kind,
        int fatCount,
        Dictionary<int, OverlayPath> result)
    {
        if (table.Length % 0x20 != 0)
            throw new InvalidDataException($"Tabela {prefix} possui tamanho inválido 0x{table.Length:X}.");

        for (var pos = 0; pos < table.Length; pos += 0x20)
        {
            var id = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(pos, 4));
            var fileId = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(pos + 0x18, 4));
            if ((uint)fileId >= (uint)fatCount)
                throw new InvalidDataException($"{prefix}_{id:000} referencia FileId inválido {fileId}.");

            result[fileId] = new OverlayPath($"sys/ovl/{prefix}_{id:000}", kind);
        }
    }

    private static FatEntry[] ReadFat(byte[] rom, uint fatOffset, int count)
    {
        RequireRange(rom.LongLength, fatOffset, checked((uint)(count * 8)), "FAT");
        var entries = new FatEntry[count];
        var pos = checked((int)fatOffset);
        for (var i = 0; i < count; i++, pos += 8)
        {
            var begin = BinaryPrimitives.ReadUInt32LittleEndian(rom.AsSpan(pos, 4));
            var end = BinaryPrimitives.ReadUInt32LittleEndian(rom.AsSpan(pos + 4, 4));
            if (end < begin)
                throw new InvalidDataException($"FAT[{i}] possui fim menor que início.");
            RequireRange(rom.LongLength, begin, end - begin, $"FAT[{i}]");
            entries[i] = new FatEntry(begin, end);
        }
        return entries;
    }

    private static void ValidateHeaderRegions(NdsHeader h, long romLength)
    {
        RequireRange(romLength, h.Arm9Offset, h.Arm9Size, "ARM9");
        RequireRange(romLength, h.Arm7Offset, h.Arm7Size, "ARM7");
        RequireRange(romLength, h.FntOffset, h.FntSize, "FNT");
        RequireRange(romLength, h.FatOffset, h.FatSize, "FAT");
        if (h.Arm9OverlaySize != 0)
            RequireRange(romLength, h.Arm9OverlayOffset, h.Arm9OverlaySize, "ARM9 overlay table");
        if (h.Arm7OverlaySize != 0)
            RequireRange(romLength, h.Arm7OverlayOffset, h.Arm7OverlaySize, "ARM7 overlay table");
    }

    private static void RequireRange(long totalLength, uint offset, uint size, string name)
    {
        var end = (ulong)offset + size;
        if (end > (ulong)totalLength)
            throw new InvalidDataException($"{name} fora dos limites da ROM: offset=0x{offset:X}, size=0x{size:X}, ROM=0x{totalLength:X}.");
    }

    private static byte CalculateDeviceCapacity(byte original, ulong usedSize)
    {
        var value = original;
        ulong capacity = 128UL * 1024UL;
        if (value < 63)
            capacity <<= value;
        else
            capacity = ulong.MaxValue;

        while (capacity < usedSize)
        {
            if (value == byte.MaxValue || capacity > ulong.MaxValue / 2)
                throw new InvalidDataException("ROM grande demais para representar no Device Capacity do header NDS.");
            value++;
            capacity <<= 1;
        }

        return value;
    }

    private static string ToDiskPath(string root, string virtualPath)
    {
        var parts = NdsRomFile.Normalize(virtualPath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Aggregate(root, (current, part) => Path.Combine(current, part));
    }

    private static byte[] Slice(byte[] source, uint offset, uint size)
    {
        RequireRange(source.LongLength, offset, size, "slice");
        var result = new byte[checked((int)size)];
        Buffer.BlockCopy(source, checked((int)offset), result, 0, result.Length);
        return result;
    }

    private static byte[] Slice(byte[] source, int offset, int size)
    {
        if (offset < 0 || size < 0 || offset > source.Length - size)
            throw new InvalidDataException("Slice fora dos limites.");
        var result = new byte[size];
        Buffer.BlockCopy(source, offset, result, 0, size);
        return result;
    }

    private static byte[] ReadExactly(Stream stream, int count)
    {
        var data = new byte[count];
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(data, total, count - total);
            if (read == 0)
                throw new EndOfStreamException();
            total += read;
        }
        return data;
    }

    private static void EnsurePosition(Stream output, uint position, byte fill)
    {
        if (output.Position > position)
            throw new InvalidDataException($"Layout ultrapassou o ARM9 offset original 0x{position:X}.");
        if (output.Position < position)
            WriteFill(output, checked((int)(position - output.Position)), fill);
    }

    private static void Pad(Stream output, int alignment, byte fill)
    {
        var padding = (alignment - (int)(output.Position % alignment)) % alignment;
        WriteFill(output, padding, fill);
    }

    private static void WriteFill(Stream output, int count, byte value)
    {
        if (count <= 0)
            return;

        Span<byte> block = stackalloc byte[4096];
        block.Fill(value);
        while (count > 0)
        {
            var chunk = Math.Min(count, block.Length);
            output.Write(block[..chunk]);
            count -= chunk;
        }
    }

    private static uint CheckedU32(long value)
    {
        if (value < 0 || value > uint.MaxValue)
            throw new InvalidDataException($"Offset/tamanho fora de UInt32: {value}.");
        return (uint)value;
    }

    private readonly record struct FatEntry(uint Begin, uint End);
    private readonly record struct OverlayPath(string Path, NdsRomFileKind Kind);
}
