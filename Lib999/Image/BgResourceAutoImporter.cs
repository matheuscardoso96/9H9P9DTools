using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lib999.Image
{
    /// <summary>
    /// Builds an import index from the entire c_999\root\bg tree.
    ///
    /// Priority is the same as export:
    /// 1. action.fld owns its directory subtree and declares the resources there.
    /// 2. DATs outside FLD-owned subtrees are probed directly with SirBg.
    /// 3. The TXT list is only a fallback handled later by Program.cs.
    /// </summary>
    public static class BgResourceAutoImporter
    {
        public static BgResourceImportIndex Build(string bgRoot)
        {
            var index = new BgResourceImportIndex();

            if (string.IsNullOrWhiteSpace(bgRoot) || !Directory.Exists(bgRoot))
            {
                WriteWarning(
                    $"Diretório de BG não encontrado para montar índice de importação: {bgRoot}");
                return index;
            }

            string normalizedBgRoot = NormalizePhysicalPath(bgRoot);
            string sourceRoot = FindC999Root(normalizedBgRoot);

            string[] fldPaths = Directory
                .GetFiles(normalizedBgRoot, "action.fld", SearchOption.AllDirectories)
                .Select(NormalizePhysicalPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var fldRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // PASSO 1: resources declarados por todos os FLDs.
            foreach (string fldPath in fldPaths)
            {
                string? fldRoot = Path.GetDirectoryName(fldPath);
                if (!string.IsNullOrWhiteSpace(fldRoot))
                    fldRoots.Add(NormalizePhysicalPath(fldRoot));

                IndexByFld(fldPath, sourceRoot, index);
            }

            // PASSO 2: DATs fora de qualquer árvore controlada por FLD.
            string[] datFiles = Directory
                .GetFiles(normalizedBgRoot, "*.dat", SearchOption.AllDirectories)
                .Select(NormalizePhysicalPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            int acceptedWithoutFld = 0;

            foreach (string sourceDatPath in datFiles)
            {
                if (IsInsideFldOwnedTree(
                    sourceDatPath,
                    normalizedBgRoot,
                    fldRoots))
                {
                    continue;
                }

                // Sem FLD, só indexa DATs que SirBg reconhece/valida.
                try
                {
                    _ = new SirBg(sourceDatPath);
                }
                catch
                {
                    continue;
                }

                string logicalDatPath =
                    BuildLogicalPath(sourceRoot, sourceDatPath);

                index.Add(new BgImportResource(
                    LogicalDatPath: logicalDatPath,
                    SourceDatPath: sourceDatPath,
                    SourceKind: BgImportSourceKind.ResourceScan,
                    ActionFldPath: null,
                    ResourceId: null));

                acceptedWithoutFld++;
            }

            Console.WriteLine(
                $"Índice de importação BG sem FLD: " +
                $"{acceptedWithoutFld} BG(s) compatível(is).");

            return index;
        }

        private static void IndexByFld(
            string fldPath,
            string sourceRoot,
            BgResourceImportIndex index)
        {
            ActionFld fld;

            try
            {
                fld = new ActionFld(fldPath);
            }
            catch (Exception ex)
            {
                WriteWarning(
                    $"Falha ao ler '{fldPath}' para importação: {ex.Message}");
                return;
            }

            var resources = fld.GetResources();

            Console.WriteLine(
                $"Índice importação FLD: {fldPath} -> " +
                $"{resources.Count} recurso(s) interno(s).");

            foreach (ActionFldResource resource in resources)
            {
                string sourceDatPath =
                    fld.ResolveResourceDataPath(resource);

                if (!File.Exists(sourceDatPath))
                {
                    WriteWarning(
                        $"Recurso listado no FLD não encontrado para importação: " +
                        $"{resource.InternalPath} -> {sourceDatPath}");
                    continue;
                }

                string logicalDatPath =
                    BuildLogicalPath(sourceRoot, sourceDatPath);

                index.Add(new BgImportResource(
                    LogicalDatPath: logicalDatPath,
                    SourceDatPath: sourceDatPath,
                    SourceKind: BgImportSourceKind.ActionFld,
                    ActionFldPath: fldPath,
                    ResourceId: resource.ResourceId));
            }
        }

        private static bool IsInsideFldOwnedTree(
            string datPath,
            string bgRoot,
            HashSet<string> fldRoots)
        {
            string? current = Path.GetDirectoryName(
                NormalizePhysicalPath(datPath));

            while (!string.IsNullOrWhiteSpace(current))
            {
                string normalizedCurrent =
                    NormalizePhysicalPath(current);

                if (fldRoots.Contains(normalizedCurrent))
                    return true;

                if (string.Equals(
                    normalizedCurrent,
                    bgRoot,
                    StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                DirectoryInfo? parent =
                    Directory.GetParent(normalizedCurrent);

                if (parent is null)
                    break;

                current = parent.FullName;
            }

            return false;
        }

        private static string FindC999Root(string path)
        {
            var current =
                new DirectoryInfo(Path.GetFullPath(path));

            while (current is not null)
            {
                if (string.Equals(
                    current.Name,
                    "c_999",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidDataException(
                $"Não foi possível localizar a raiz c_999 a partir de '{path}'.");
        }

        private static string BuildLogicalPath(
            string sourceRoot,
            string sourceDatPath)
        {
            string relative = Path.GetRelativePath(
                Path.GetFullPath(sourceRoot),
                Path.GetFullPath(sourceDatPath));

            if (relative == ".." ||
                relative.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Resource fora de c_999: {sourceDatPath}");
            }

            return NormalizeLogicalPath(
                Path.Combine("999", relative));
        }

        internal static string NormalizeLogicalPath(string path)
        {
            string normalized =
                path.Replace('/', '\\').Trim();

            while (normalized.StartsWith(".\\", StringComparison.Ordinal))
                normalized = normalized[2..];

            if (normalized.StartsWith(
                "c_",
                StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            return normalized;
        }

        private static string NormalizePhysicalPath(string path)
        {
            string normalizedSeparators = path
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            return Path.GetFullPath(normalizedSeparators)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }

        private static void WriteWarning(string text)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(text);
            Console.ForegroundColor = oldColor;
        }
    }

    public sealed class BgResourceImportIndex
    {
        private readonly Dictionary<string, BgImportResource> _resources =
            new(StringComparer.OrdinalIgnoreCase);

        internal void Add(BgImportResource resource)
        {
            string key =
                BgResourceAutoImporter.NormalizeLogicalPath(
                    resource.LogicalDatPath);

            if (_resources.TryGetValue(
                key,
                out BgImportResource? current))
            {
                if (string.Equals(
                    Path.GetFullPath(current.SourceDatPath),
                    Path.GetFullPath(resource.SourceDatPath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // FLD sempre vence um scan genérico.
                if (current.SourceKind ==
                    BgImportSourceKind.ActionFld)
                    return;

                if (resource.SourceKind ==
                    BgImportSourceKind.ActionFld)
                {
                    _resources[key] = resource;
                    return;
                }

                return;
            }

            _resources.Add(key, resource);
        }

        public bool TryGetByEditedFile(
            string editedFilePath,
            string editedRoot,
            out BgImportResource resource)
        {
            resource = default!;

            string rootFullPath =
                Path.GetFullPath(editedRoot);

            string fileFullPath =
                Path.GetFullPath(editedFilePath);

            string relative =
                Path.GetRelativePath(rootFullPath, fileFullPath);

            if (relative == ".." ||
                relative.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (relative.EndsWith(
                ".png",
                StringComparison.OrdinalIgnoreCase))
            {
                relative = relative[..^4];
            }

            string key =
                BgResourceAutoImporter.NormalizeLogicalPath(relative);

            return _resources.TryGetValue(key, out resource!);
        }

        public int Count => _resources.Count;

        public int FldCount =>
            _resources.Values.Count(
                x => x.SourceKind == BgImportSourceKind.ActionFld);

        public int ScanCount =>
            _resources.Values.Count(
                x => x.SourceKind == BgImportSourceKind.ResourceScan);
    }

    public enum BgImportSourceKind
    {
        ActionFld,
        ResourceScan
    }

    public sealed record BgImportResource(
        string LogicalDatPath,
        string SourceDatPath,
        BgImportSourceKind SourceKind,
        string? ActionFldPath,
        uint? ResourceId);
}
