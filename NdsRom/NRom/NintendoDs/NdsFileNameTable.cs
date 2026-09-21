using System.Buffers.Binary;
using System.Text;

namespace NdsRom.NRom.NintendoDs;

/// <summary>
/// Read-only parser for the Nintendo DS File Name Table (FNT).
/// The rebuilder intentionally preserves the original FNT bytes, so filenames
/// never need to be re-encoded and Shift-JIS names remain byte-identical.
/// </summary>
internal static class NdsFileNameTable
{
    private readonly record struct DirectoryRecord(uint SubTableOffset, ushort FirstFileId, ushort ParentOrCount);

    private static readonly Encoding ShiftJis = CreateShiftJis();

    public static Dictionary<int, string> Parse(ReadOnlySpan<byte> fnt, int fatEntryCount)
    {
        if (fnt.Length < 8)
            throw new InvalidDataException("FNT inválida: menos de 8 bytes.");

        var root = ReadDirectoryRecord(fnt, 0);
        var directoryCount = root.ParentOrCount;
        if (directoryCount == 0 || directoryCount * 8 > fnt.Length)
            throw new InvalidDataException($"FNT inválida: quantidade de diretórios = {directoryCount}.");

        var directories = new DirectoryRecord[directoryCount];
        for (var i = 0; i < directories.Length; i++)
            directories[i] = ReadDirectoryRecord(fnt, i * 8);

        var result = new Dictionary<int, string>();
        var activeDirectories = new HashSet<int>();
        ParseDirectory(0, string.Empty, fnt, directories, fatEntryCount, result, activeDirectories);
        return result;
    }

    private static void ParseDirectory(
        int directoryIndex,
        string parentPath,
        ReadOnlySpan<byte> fnt,
        DirectoryRecord[] directories,
        int fatEntryCount,
        Dictionary<int, string> result,
        HashSet<int> activeDirectories)
    {
        if ((uint)directoryIndex >= (uint)directories.Length)
            throw new InvalidDataException($"FNT referencia diretório inválido: {directoryIndex}.");

        if (!activeDirectories.Add(directoryIndex))
            throw new InvalidDataException("FNT contém referência recursiva de diretório.");

        try
        {
            var directory = directories[directoryIndex];
            var pos = checked((int)directory.SubTableOffset);
            var fileId = (int)directory.FirstFileId;

            while (true)
            {
                Require(fnt, pos, 1);
                var typeLength = fnt[pos++];
                if (typeLength == 0)
                    break;
                if (typeLength == 0x80)
                    throw new InvalidDataException("FNT contém TypeLength reservado 0x80.");

                var isDirectory = (typeLength & 0x80) != 0;
                var nameLength = typeLength & 0x7F;
                Require(fnt, pos, nameLength);

                var name = ShiftJis.GetString(fnt.Slice(pos, nameLength));
                pos += nameLength;

                if (!isDirectory)
                {
                    if ((uint)fileId >= (uint)fatEntryCount)
                        throw new InvalidDataException($"FNT referencia FileId {fileId}, mas a FAT possui {fatEntryCount} entradas.");

                    result[fileId++] = Combine(parentPath, name);
                    continue;
                }

                Require(fnt, pos, 2);
                var directoryId = BinaryPrimitives.ReadUInt16LittleEndian(fnt.Slice(pos, 2));
                pos += 2;
                var childIndex = directoryId & 0x0FFF;

                ParseDirectory(
                    childIndex,
                    Combine(parentPath, name),
                    fnt,
                    directories,
                    fatEntryCount,
                    result,
                    activeDirectories);
            }
        }
        finally
        {
            activeDirectories.Remove(directoryIndex);
        }
    }

    private static DirectoryRecord ReadDirectoryRecord(ReadOnlySpan<byte> fnt, int offset)
    {
        Require(fnt, offset, 8);
        return new DirectoryRecord(
            BinaryPrimitives.ReadUInt32LittleEndian(fnt.Slice(offset, 4)),
            BinaryPrimitives.ReadUInt16LittleEndian(fnt.Slice(offset + 4, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(fnt.Slice(offset + 6, 2)));
    }

    private static string Combine(string left, string right) =>
        string.IsNullOrEmpty(left) ? right : $"{left}/{right}";

    private static void Require(ReadOnlySpan<byte> data, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count)
            throw new InvalidDataException("FNT truncada ou com offsets inválidos.");
    }

    private static Encoding CreateShiftJis()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
    }
}
