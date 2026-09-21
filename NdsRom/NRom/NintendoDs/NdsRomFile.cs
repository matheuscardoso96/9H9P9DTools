namespace NdsRom.NRom.NintendoDs;

public enum NdsRomFileKind
{
    NitroFs,
    Arm9,
    Arm7,
    Banner,
    Arm9Overlay,
    Arm7Overlay,
    UnknownFatEntry
}

/// <summary>
/// One replaceable file exposed by the NDS archive.
/// FAT-backed entries keep their original file ID; system files do not use FAT IDs.
/// </summary>
public sealed class NdsRomFile
{
    internal NdsRomFile(string virtualPath, NdsRomFileKind kind, byte[] data, int? fileId = null)
    {
        VirtualPath = Normalize(virtualPath);
        Kind = kind;
        Data = data;
        FileId = fileId;
    }

    public string VirtualPath { get; }
    public NdsRomFileKind Kind { get; }
    public int? FileId { get; }
    public byte[] Data { get; set; }
    public long FileSize => Data.LongLength;

    internal static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
