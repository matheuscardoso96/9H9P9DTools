using NdsRom.NRom.NintendoDs;

namespace NdsRom.NRom;

/// <summary>
/// Compatibilidade com os callers existentes. O nome histórico da classe foi mantido,
/// mas ela não usa mais Kuriimu/Kuriimu2 nem plugin_nintendo.dll.
/// </summary>
public static class NDSKuriimuRoomTool
{
    public static Task ExportRomWithKuriimu(string inputPath, string destPath) => ExportRom(inputPath, destPath);

    public static void ImportRomWithKuriimu(string originalRomPath, string modifiedFilesPath, string outputRomPath) =>
        ImportRom(originalRomPath, modifiedFilesPath, outputRomPath);

    public static Task ExportRom(string inputPath, string destPath)
    {
        var rom = NdsRomImage.Load(inputPath);
        rom.ExtractToDirectory(destPath);

        // Mantém o comportamento antigo de criar uma cópia-base c_<pasta>.
        var fullDest = Path.GetFullPath(destPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        rom.ExtractToDirectory(fullDest.Replace("999\\root", "c_999\\root"));

        return Task.CompletedTask;
    }

    public static void ImportRom(string originalRomPath, string modifiedFilesPath, string outputRomPath)
    {
        var rom = NdsRomImage.Load(originalRomPath);
        var replaced = rom.ApplyDirectory(modifiedFilesPath);

        Console.WriteLine($"Arquivos substituídos: {replaced}");
        rom.Save(outputRomPath);

        foreach (var issue in NdsRomValidator.ValidateFile(outputRomPath))
            Console.WriteLine(issue.IsError ? $"[ERRO] {issue.Message}" : $"[OK] {issue.Message}");
    }

    // Compare old ROM with new ROM and show differences in exposed files.
    public static void CompareRoms(string oldRomPath, string newRomPath)
    {
        var oldRom = NdsRomImage.Load(oldRomPath);
        var newRom = NdsRomImage.Load(newRomPath);

        var oldFiles = oldRom.Files.ToDictionary(x => x.VirtualPath, StringComparer.OrdinalIgnoreCase);
        var newFiles = newRom.Files.ToDictionary(x => x.VirtualPath, StringComparer.OrdinalIgnoreCase);
        var differences = new List<string>();

        foreach (var (path, oldFile) in oldFiles)
        {
            if (!newFiles.TryGetValue(path, out var newFile))
            {
                differences.Add($"File missing in new ROM: {path}");
                continue;
            }

            if (oldFile.FileSize != newFile.FileSize)
            {
                differences.Add($"File size mismatch: {path} (Old: {oldFile.FileSize}, New: {newFile.FileSize})");
                continue;
            }

            if (!oldFile.Data.AsSpan().SequenceEqual(newFile.Data))
                differences.Add($"File content mismatch: {path}");
        }

        foreach (var path in newFiles.Keys)
        {
            if (!oldFiles.ContainsKey(path))
                differences.Add($"New file added in new ROM: {path}");
        }

        if (differences.Count == 0)
        {
            Console.WriteLine("No differences found between the ROMs.");
            return;
        }

        Console.WriteLine("Differences found:");
        foreach (var difference in differences)
            Console.WriteLine(difference);
    }

    public static IReadOnlyList<NdsValidationIssue> ValidateRom(string romPath) =>
        NdsRomValidator.ValidateFile(romPath);
}
