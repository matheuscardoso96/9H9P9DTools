using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public enum AutomaticProjectResourceKind
{
    Font,
    HistoryFsb,
    FileTexts,
    SystemTexts,
    ItemsNames,
    CameraTexts,
    CharaTexts,
    RoomTexts,
    StaffTexts
}

public sealed record AutomaticProjectResource(
    AutomaticProjectResourceKind Kind,
    string SourcePath)
{
    public string LogicalSourcePath =>
        SourcePath.StartsWith("c_", StringComparison.OrdinalIgnoreCase)
            ? SourcePath[2..]
            : SourcePath;

    public string PrimaryEditedPath => $"{LogicalSourcePath}.txt";

    public string ExportFlag => Kind switch
    {
        AutomaticProjectResourceKind.Font => "-fe",
        AutomaticProjectResourceKind.HistoryFsb => "-fsbe",
        AutomaticProjectResourceKind.FileTexts => "-dattextv1e",
        AutomaticProjectResourceKind.SystemTexts => "-dattextv4e",
        AutomaticProjectResourceKind.ItemsNames => "-itemstextse",
        AutomaticProjectResourceKind.CameraTexts => "-cameratextse",
        AutomaticProjectResourceKind.CharaTexts => "-charatextse",
        AutomaticProjectResourceKind.RoomTexts => "-romtextse",
        AutomaticProjectResourceKind.StaffTexts => "-romtextse",
        _ => throw new ArgumentOutOfRangeException()
    };

    public string ImportFlag => Kind switch
    {
        AutomaticProjectResourceKind.Font => "-fi",
        AutomaticProjectResourceKind.HistoryFsb => "-fsbi",
        AutomaticProjectResourceKind.FileTexts => "-dattextv1i",
        AutomaticProjectResourceKind.SystemTexts => "-dattextv4i",
        AutomaticProjectResourceKind.ItemsNames => "-itemstextsi",
        AutomaticProjectResourceKind.CameraTexts => "-cameratextsi",
        AutomaticProjectResourceKind.CharaTexts => "-charatextsi",
        AutomaticProjectResourceKind.RoomTexts => "-romtextse",
        AutomaticProjectResourceKind.StaffTexts => "-romtextse",
        _ => throw new ArgumentOutOfRangeException()
    };

    public string CreateExportArgument() => $"{SourcePath}, {ExportFlag}";
    public string CreateImportArgument() => $"{SourcePath}, {ImportFlag}";
}

public static class AutomaticProjectResourceCatalog
{
    public static IReadOnlyList<AutomaticProjectResource> Discover(string c999Root = "c_999")
    {
        var resources = new Dictionary<string, AutomaticProjectResource>(
            StringComparer.OrdinalIgnoreCase);

        string root = ToProjectRelative(c999Root);
        string etcDirectory = Path.Combine(root, "root", "etc");
        string scrDirectory = Path.Combine(root, "root", "scr");

        if (Directory.Exists(etcDirectory))
        {
            foreach (string path in Directory
                .GetFiles(etcDirectory, "kanji_*.dat", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                Add(resources, AutomaticProjectResourceKind.Font, path);
            }

            AddIfExists(resources, AutomaticProjectResourceKind.FileTexts, Path.Combine(etcDirectory, "file.dat"));
            AddIfExists(resources, AutomaticProjectResourceKind.SystemTexts, Path.Combine(etcDirectory, "sysmes.dat"));
            AddIfExists(resources, AutomaticProjectResourceKind.ItemsNames, Path.Combine(etcDirectory, "item.dat"));
            AddIfExists(resources, AutomaticProjectResourceKind.CameraTexts, Path.Combine(etcDirectory, "camera.dat"));
            AddIfExists(resources, AutomaticProjectResourceKind.CharaTexts, Path.Combine(etcDirectory, "chara.dat"));
            AddIfExists(resources, AutomaticProjectResourceKind.RoomTexts, Path.Combine(etcDirectory, "room.dat"));
            AddIfExists(resources, AutomaticProjectResourceKind.RoomTexts, Path.Combine(etcDirectory, "staff.dat"));
        }

        if (Directory.Exists(scrDirectory))
        {
            foreach (string path in Directory
                .GetFiles(scrDirectory, "*.fsb", SearchOption.AllDirectories)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                Add(resources, AutomaticProjectResourceKind.HistoryFsb, path);
            }
        }

        return resources.Values
            .OrderBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static AutomaticProjectResourceImportIndex BuildImportIndex(string c999Root = "c_999")
        => new(Discover(c999Root));

    private static void AddIfExists(
        IDictionary<string, AutomaticProjectResource> resources,
        AutomaticProjectResourceKind kind,
        string path)
    {
        if (File.Exists(path))
            Add(resources, kind, path);
    }

    private static void Add(
        IDictionary<string, AutomaticProjectResource> resources,
        AutomaticProjectResourceKind kind,
        string path)
    {
        string relativePath = ToProjectRelative(path);
        string key = NormalizeSourcePath(relativePath);
        resources[key] = new AutomaticProjectResource(kind, relativePath);
    }

    internal static string ExtractSourcePath(string arg)
    {
        string firstPart = arg.Split(',')[0].Trim();
        int metadataIndex = firstPart.IndexOf('@');
        if (metadataIndex >= 0)
            firstPart = firstPart[..metadataIndex];

        return firstPart.Trim();
    }

    internal static string NormalizeSourcePath(string path)
    {
        string normalizedSeparators = path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        try
        {
            return Path.GetFullPath(normalizedSeparators)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return normalizedSeparators;
        }
    }

    internal static string NormalizeLogicalPath(string path)
    {
        string normalized = path.Replace('/', '\\').Trim();

        while (normalized.StartsWith(".\\", StringComparison.Ordinal))
            normalized = normalized[2..];

        if (normalized.StartsWith("c_", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];

        return normalized;
    }

    private static string ToProjectRelative(string path)
    {
        string workingDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
        string fullPath = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(workingDirectory, fullPath);

        if (relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"O recurso '{path}' está fora do diretório de trabalho '{workingDirectory}'.");
        }

        return relative;
    }
}

public sealed class AutomaticProjectResourceExportResult
{
    private readonly HashSet<string> _exportedPaths =
        new(StringComparer.OrdinalIgnoreCase);

    public void AddExported(AutomaticProjectResource resource)
    {
        _exportedPaths.Add(
            AutomaticProjectResourceCatalog.NormalizeSourcePath(resource.SourcePath));
    }

    public bool WasExported(string fileExportArg)
    {
        string sourcePath =
            AutomaticProjectResourceCatalog.ExtractSourcePath(fileExportArg);

        return _exportedPaths.Contains(
            AutomaticProjectResourceCatalog.NormalizeSourcePath(sourcePath));
    }

    public int ExportedCount => _exportedPaths.Count;
}

public sealed class AutomaticProjectResourceImportIndex
{
    private readonly Dictionary<string, AutomaticProjectResource> _byEditedPath =
        new(StringComparer.OrdinalIgnoreCase);

    public AutomaticProjectResourceImportIndex(
        IEnumerable<AutomaticProjectResource> resources)
    {
        foreach (AutomaticProjectResource resource in resources)
        {
            string key =
                AutomaticProjectResourceCatalog.NormalizeLogicalPath(
                    resource.PrimaryEditedPath);

            _byEditedPath[key] = resource;
        }
    }

    public bool TryGetByEditedFile(
        string editedFilePath,
        string editedRoot,
        out AutomaticProjectResource resource)
    {
        resource = default!;

        string rootFullPath = Path.GetFullPath(editedRoot);
        string fileFullPath = Path.GetFullPath(editedFilePath);
        string relative = Path.GetRelativePath(rootFullPath, fileFullPath);

        if (relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return false;
        }

        string key =
            AutomaticProjectResourceCatalog.NormalizeLogicalPath(relative);

        return _byEditedPath.TryGetValue($"999\\{key}", out resource!);
    }

    public int Count => _byEditedPath.Count;
}
