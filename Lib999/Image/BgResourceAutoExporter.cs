using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lib999.Image
{
    public static class BgResourceAutoExporter
    {
        public static BgResourceAutoExportResult Export(string bgRoot)
        {
            var result = new BgResourceAutoExportResult();

            if (string.IsNullOrWhiteSpace(bgRoot) || !Directory.Exists(bgRoot))
            {
                WriteWarning($"Diretório de BG não encontrado: {bgRoot}");
                return result;
            }

            string normalizedBgRoot = NormalizeSourcePath(bgRoot);

            string[] fldPaths = Directory
                .GetFiles(normalizedBgRoot, "action.fld", SearchOption.AllDirectories)
                .Select(NormalizeSourcePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var fldRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) FLD tem prioridade.
            foreach (string fldPath in fldPaths)
            {
                string? fldRoot = Path.GetDirectoryName(fldPath);
                if (!string.IsNullOrWhiteSpace(fldRoot))
                    fldRoots.Add(NormalizeSourcePath(fldRoot));

                ExportByFld(fldPath, result);
            }

            // 2) DATs fora de qualquer árvore controlada por FLD.
            string[] datFiles = Directory
                .GetFiles(normalizedBgRoot, "*.dat", SearchOption.AllDirectories)
                .Select(NormalizeSourcePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            int scannedWithoutFld = 0;
            int exportedWithoutFld = 0;

            foreach (string datPath in datFiles)
            {

                string normalized = NormalizeSourcePath(datPath);

                if (result.ContainsExportedPath(normalized))
                    continue;

                if (IsInsideFldOwnedTree(datPath, normalizedBgRoot, fldRoots))
                    continue;

                scannedWithoutFld++;

                try
                {
                    var bg = new SirBg(datPath);

                    Console.WriteLine(
                        $"Exportando bg via scan sem FLD: {bg.FileName} " +
                        $"({bg.Width}x{bg.Height}, {bg.ColorDep} bpp)");

                    bg.SirBgToPng(datPath);
                    result.AddExportedPath(normalized);
                    exportedWithoutFld++;
                }
                catch
                {
                    // root\bg também contém DATs que não são BG desse formato.
                }
            }

            Console.WriteLine(
                $"Scan de BG sem FLD: {scannedWithoutFld} DAT(s) testado(s), " +
                $"{exportedWithoutFld} BG(s) exportado(s).");

            return result;
        }

        private static void ExportByFld(
            string fldPath,
            BgResourceAutoExportResult result)
        {
            result.AddProcessedFld(fldPath);

            ActionFld fld;
            try
            {
                fld = new ActionFld(fldPath);
            }
            catch (Exception ex)
            {
                WriteWarning($"Falha ao ler '{fldPath}': {ex.Message}");
                return;
            }

            var resources = fld.GetResources();

            Console.WriteLine(
                $"FLD: {fldPath} -> {resources.Count} recurso(s) interno(s).");

            foreach (ActionFldResource resource in resources)
            {
                string datPath = fld.ResolveResourceDataPath(resource);
                string normalized = NormalizeSourcePath(datPath);

                if (result.ContainsExportedPath(normalized))
                    continue;

                if (!File.Exists(datPath))
                {
                    WriteWarning(
                        $"Recurso listado no FLD não encontrado: " +
                        $"{resource.InternalPath} -> {datPath}");
                    continue;
                }

                try
                {
                    var bg = new SirBg(datPath);

                    Console.WriteLine(
                        $"Exportando bg via action.fld: {bg.FileName} " +
                        $"({bg.Width}x{bg.Height}, {bg.ColorDep} bpp)");

                    bg.SirBgToPng(datPath);
                    result.AddExportedPath(normalized);
                }
                catch (Exception ex)
                {
                    // Não marca como exportado; a lista TXT ainda pode ser fallback.
                    WriteWarning(
                        $"Não foi possível exportar automaticamente '{datPath}' " +
                        $"pelo action.fld: {ex.Message}");
                }
            }
        }

        public static bool IsActionFldBgEntry(string arg)
        {
            if (!arg.Contains("-bge", StringComparison.OrdinalIgnoreCase))
                return false;

            string sourcePath = ExtractSourcePath(arg);

            return string.Equals(
                Path.GetFileName(sourcePath),
                "action.fld",
                StringComparison.OrdinalIgnoreCase);
        }

        public static string ExtractSourcePath(string arg)
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
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return normalizedSeparators;
            }
        }

        private static bool IsInsideFldOwnedTree(
            string datPath,
            string bgRoot,
            HashSet<string> fldRoots)
        {
            string? current =
                Path.GetDirectoryName(NormalizeSourcePath(datPath));

            while (!string.IsNullOrWhiteSpace(current))
            {
                string normalizedCurrent =
                    NormalizeSourcePath(current);

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

        private static void WriteWarning(string text)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(text);
            Console.ForegroundColor = oldColor;
        }
    }

    public sealed class BgResourceAutoExportResult
    {
        private readonly HashSet<string> _exportedPaths =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _processedFldPaths =
            new(StringComparer.OrdinalIgnoreCase);

        internal void AddExportedPath(string path) =>
            _exportedPaths.Add(
                BgResourceAutoExporter.NormalizeSourcePath(path));

        internal bool ContainsExportedPath(string path) =>
            _exportedPaths.Contains(
                BgResourceAutoExporter.NormalizeSourcePath(path));

        internal void AddProcessedFld(string path) =>
            _processedFldPaths.Add(
                BgResourceAutoExporter.NormalizeSourcePath(path));

        public bool WasExported(string bgArg)
        {
            string path =
                BgResourceAutoExporter.ExtractSourcePath(bgArg);

            return _exportedPaths.Contains(
                BgResourceAutoExporter.NormalizeSourcePath(path));
        }

        public bool IsProcessedActionFld(string bgArg)
        {
            string path =
                BgResourceAutoExporter.ExtractSourcePath(bgArg);

            return _processedFldPaths.Contains(
                BgResourceAutoExporter.NormalizeSourcePath(path));
        }

        public int ExportedCount => _exportedPaths.Count;
    }
}
