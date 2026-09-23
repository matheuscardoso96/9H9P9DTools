using System.Text;

namespace Lib999.Image
{
    /// <summary>
    /// Reader for the SIR0 action.fld files used by 999 minigames.
    ///
    /// The root points to a 0x28-byte record table. kind == 0 records are
    /// drawable objects (logical width/height) and kind == 2 records are
    /// external resources such as ./resource/icon_quit.
    ///
    /// Important: an object's action block can reference MORE THAN ONE resource.
    /// The resource id is stored in the first dword of each 0x20-byte action entry,
    /// until the 0xFFFFFF01 terminator. The old parser checked only the first dword
    /// of the block, which missed entries such as button_c/button_caction.
    /// </summary>
    public sealed class ActionFld
    {
        private const uint Sir0Magic = 0x30524953; // "SIR0"
        private const uint ActionListTerminator = 0xFFFFFF01;
        private const int RecordSize = 0x28;
        private const int ActionEntrySize = 0x20;

        private static readonly Encoding ShiftJisEncoding = CreateShiftJisEncoding();

        private readonly byte[] _data;
        private readonly uint _recordsOffset;
        private readonly uint _actionDataOffset;
        private readonly uint _mappingTableOffset;
        private IReadOnlyList<ActionFldResource>? _resources;
        private IReadOnlyList<ActionFldObjectAction>? _objectActions;

        public string FilePath { get; }

        public ActionFld(string filePath)
        {
            FilePath = filePath;
            _data = File.ReadAllBytes(filePath);

            using var br = new BinaryReader(new MemoryStream(_data), ShiftJisEncoding, leaveOpen: false);
            var header = new SirHeader(br);

            if ((uint)header.Magic != Sir0Magic)
                throw new InvalidDataException($"'{filePath}' não possui header SIR0 válido.");

            if (header.Offset0 < 0 || (long)header.Offset0 + 0x10 > _data.Length)
                throw new InvalidDataException($"Root SIR0 inválido em '{filePath}'.");

            // action.fld root observed in 999:
            // +0x00 -> first 0x28-byte object/resource record
            // +0x04 -> beginning of action data (also marks end of record table)
            // +0x08 -> table of (objectId, actionDataOffset) pairs
            // +0x0C -> another action table (not needed here)
            _recordsOffset = ReadUInt32(header.Offset0 + 0x00);
            _actionDataOffset = ReadUInt32(header.Offset0 + 0x04);
            _mappingTableOffset = ReadUInt32(header.Offset0 + 0x08);

            ValidateOffset(_recordsOffset, nameof(_recordsOffset));
            ValidateOffset(_actionDataOffset, nameof(_actionDataOffset), allowEnd: true);
            ValidateOffset(_mappingTableOffset, nameof(_mappingTableOffset));

            if (_recordsOffset >= _actionDataOffset)
                throw new InvalidDataException($"Tabela de records inválida em '{filePath}'.");
        }

        /// <summary>
        /// Returns all kind==2 entries that point to ./resource/...
        /// </summary>
        public IReadOnlyList<ActionFldResource> GetResources()
        {
            if (_resources is not null)
                return _resources;

            var resources = new List<ActionFldResource>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (uint offset = _recordsOffset;
                 (long)offset + RecordSize <= _actionDataOffset;
                 offset += RecordSize)
            {
                uint id = ReadUInt32(offset + 0x00);
                uint kind = ReadUInt32(offset + 0x04);

                if (kind != 2)
                    continue;

                uint pathOffset = ReadUInt32(offset + 0x18);
                string? internalPath = TryReadNullTerminatedShiftJis(pathOffset);

                if (string.IsNullOrWhiteSpace(internalPath))
                    continue;

                internalPath = NormalizeResourcePath(internalPath);
                if (!internalPath.StartsWith("./resource/", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!seenPaths.Add(internalPath))
                    continue;

                var dimensions = GetObjectDimensionsForResourceId(id);
                int? logicalWidth = null;
                int? logicalHeight = null;

                // Keep the old convenience fields only when the link resolves to one
                // distinct logical size. Full candidate handling is available through
                // GetResourceDimensionCandidates().
                if (dimensions.Count == 1)
                {
                    logicalWidth = dimensions[0].Width;
                    logicalHeight = dimensions[0].Height;
                }

                resources.Add(new ActionFldResource(id, internalPath, logicalWidth, logicalHeight));
            }

            _resources = resources;
            return _resources;
        }

        public bool TryGetResourceDimensions(string resourceFileName, out int width, out int height)
        {
            width = 0;
            height = 0;

            var candidates = GetResourceDimensionCandidates(resourceFileName);
            if (candidates.Count != 1)
                return false;

            width = candidates[0].Width;
            height = candidates[0].Height;
            return true;
        }

        /// <summary>
        /// Returns every distinct logical object size whose action list actually references
        /// the requested resource. Unlike the old code, this scans all 0x20-byte entries
        /// before the action-list terminator instead of testing only the first dword.
        /// </summary>
        public IReadOnlyList<ActionFldDimensions> GetResourceDimensionCandidates(string resourceFileName)
        {
            // Resource names inside action.fld are logical names, not normal Windows
            // file names. A dot can be part of the resource name (for example
            // "53.2kg"), so Path.GetFileNameWithoutExtension() is wrong here: it
            // would turn "53.2kg" into "53". Strip only the real .dat suffix.
            string wantedName = GetResourceKey(resourceFileName);
            if (string.IsNullOrWhiteSpace(wantedName))
                return Array.Empty<ActionFldDimensions>();

            ActionFldResource? resource = GetResources().FirstOrDefault(r =>
                string.Equals(
                    GetResourceKey(r.InternalPath),
                    wantedName,
                    StringComparison.OrdinalIgnoreCase));

            if (resource is null)
                return Array.Empty<ActionFldDimensions>();

            return GetObjectDimensionsForResourceId(resource.ResourceId);
        }

        /// <summary>
        /// Resolve dimensions using only object(s) that reference this exact resource.
        /// Exact aligned matches are preferred. If there is no exact match, a conservative
        /// derived width/height is attempted using the image byte count, but only succeeds
        /// when that produces exactly one distinct result.
        /// </summary>
        public bool TryInferStoredDimensionsForResource(
            string resourceFileName,
            uint imageSize,
            int colorDepth,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;

            if (colorDepth is not (4 or 8))
                return false;

            var logicalCandidates = GetResourceDimensionCandidates(resourceFileName);
            if (logicalCandidates.Count == 0)
                return false;

            var exactMatches = new HashSet<(int Width, int Height)>();

            foreach (ActionFldDimensions logical in logicalCandidates)
            {
                int storedWidth = Align8(logical.Width);
                int storedHeight = Align8(logical.Height);

                if (ImageByteCountFor(storedWidth, storedHeight, colorDepth) == imageSize)
                    exactMatches.Add((storedWidth, storedHeight));
            }

            if (exactMatches.Count == 1)
            {
                var match = exactMatches.First();
                width = match.Width;
                height = match.Height;
                return true;
            }

            // If more than one exact size exists we must not guess.
            if (exactMatches.Count > 1)
                return false;

            // Some FLD objects describe the on-screen/logical rectangle rather than the
            // exact raw bitmap dimensions. In those cases Align8(logicalWidth/Height) is
            // not enough. Example observed in 999:
            //   logical 22x28 + 0x200-byte 8bpp image -> raw 16x32
            //   logical 124x163 + 0x4B00-byte 8bpp image -> raw 120x160
            //
            // The SIR0 image byte count + BPP gives the exact pixel AREA. Enumerate every
            // 8-pixel-aligned factor pair with that area and use the FLD rectangle only as
            // an aspect/size anchor. Accept only a close and unambiguous best match.
            var fuzzyMatches = new List<StoredDimensionCandidate>();

            foreach (ActionFldDimensions logical in logicalCandidates)
            {
                foreach (var stored in EnumerateAlignedDimensions(imageSize, colorDepth))
                {
                    if (!IsReasonablyCloseToLogical(logical, stored.Width, stored.Height))
                        continue;

                    double score = DimensionDistanceScore(logical, stored.Width, stored.Height);
                    fuzzyMatches.Add(new StoredDimensionCandidate(
                        stored.Width,
                        stored.Height,
                        logical.Width,
                        logical.Height,
                        score));
                }
            }

            if (TryChooseUniqueBestStoredDimension(fuzzyMatches, out var best))
            {
                width = best.Width;
                height = best.Height;
                return true;
            }

            return false;
        }

        private static string GetResourceKey(string path)
        {
            string normalized = path.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            string name = slash >= 0 ? normalized[(slash + 1)..] : normalized;

            // Only .dat is a transport/storage extension for these BG resources.
            // Other dots belong to the actual resource name (53.2kg, 51.3kg, ...).
            if (name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            return name;
        }

        /// <summary>
        /// Resolves a ./resource/name entry to the actual file beside action.fld.
        /// 999 normally omits the .dat extension inside the FLD.
        /// </summary>
        public string ResolveResourceDataPath(ActionFldResource resource)
        {
            string baseDirectory = Path.GetDirectoryName(FilePath)
                ?? throw new InvalidDataException($"Não foi possível obter o diretório de '{FilePath}'.");

            string relative = resource.InternalPath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            string dotPrefix = $".{Path.DirectorySeparatorChar}";
            if (relative.StartsWith(dotPrefix, StringComparison.Ordinal))
                relative = relative[dotPrefix.Length..];

            string candidate = Path.Combine(baseDirectory, relative);

            // Some FLD resource names contain dots that are NOT extensions, e.g.
            // ./resource/53.2kg -> resource\53.2kg.dat.
            // Therefore Path.HasExtension() must not be used to decide whether .dat
            // should be appended. Prefer an exact on-disk match first, then try .dat.
            if (File.Exists(candidate))
                return candidate;

            if (!candidate.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
            {
                string withDat = candidate + ".dat";
                if (File.Exists(withDat))
                    return withDat;

                // Return the path the tool expects so diagnostics show the correct
                // filename even when it is genuinely absent.
                return withDat;
            }

            return candidate;
        }

        /// <summary>
        /// Last-resort heuristic for a resource that cannot be linked to a specific object.
        /// It scans every kind==0 object and succeeds only when one distinct aligned size
        /// matches the SIR0 image byte count.
        /// </summary>
        public bool TryInferStoredDimensions(uint imageSize, int colorDepth, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (colorDepth is not (4 or 8))
                return false;

            var matches = new HashSet<(int Width, int Height)>();

            for (uint offset = _recordsOffset;
                 (long)offset + RecordSize <= _actionDataOffset;
                 offset += RecordSize)
            {
                uint kind = ReadUInt32(offset + 0x04);
                if (kind != 0)
                    continue;

                uint logicalWidth = ReadUInt32(offset + 0x08);
                uint logicalHeight = ReadUInt32(offset + 0x0C);

                if (logicalWidth == 0 || logicalHeight == 0 ||
                    logicalWidth > 4096 || logicalHeight > 4096)
                    continue;

                int storedWidth = Align8((int)logicalWidth);
                int storedHeight = Align8((int)logicalHeight);
                long expectedSize = ImageByteCountFor(storedWidth, storedHeight, colorDepth);

                if (expectedSize == imageSize)
                    matches.Add((storedWidth, storedHeight));
            }

            if (matches.Count != 1)
                return false;

            var match = matches.First();
            width = match.Width;
            height = match.Height;
            return true;
        }

        public string DescribeResourceDimensionCandidates(string resourceFileName)
        {
            var candidates = GetResourceDimensionCandidates(resourceFileName);
            if (candidates.Count == 0)
                return "nenhum vínculo objeto→resource encontrado no FLD";

            return string.Join(", ", candidates.Select(x => $"{x.Width}x{x.Height}"));
        }

        public static bool TryOpenForDat(string datPath, out ActionFld? fld)
        {
            fld = null;

            string? resourceDirectory = Path.GetDirectoryName(datPath);
            if (string.IsNullOrWhiteSpace(resourceDirectory))
                return false;

            var directory = new DirectoryInfo(resourceDirectory);

            // Normal layout is <minigame>/resource/file.dat, but walking a few levels
            // makes the reader tolerant of nested resource folders.
            for (int level = 0; directory is not null && level < 4; level++, directory = directory.Parent)
            {
                string actionFldPath = Path.Combine(directory.FullName, "action.fld");
                if (!File.Exists(actionFldPath))
                    continue;

                try
                {
                    fld = new ActionFld(actionFldPath);
                    return true;
                }
                catch (InvalidDataException)
                {
                    return false;
                }
                catch (EndOfStreamException)
                {
                    return false;
                }
            }

            return false;
        }

        public static bool TryResolveForDat(string datPath, out ActionFldResourceInfo info)
        {
            info = default;

            if (!TryOpenForDat(datPath, out var fld) || fld is null)
                return false;

            if (!fld.TryGetResourceDimensions(Path.GetFileName(datPath), out int width, out int height))
                return false;

            info = new ActionFldResourceInfo(fld.FilePath, width, height);
            return true;
        }

        private IReadOnlyList<ActionFldDimensions> GetObjectDimensionsForResourceId(uint resourceId)
        {
            var result = new HashSet<ActionFldDimensions>();

            foreach (ActionFldObjectAction objectAction in GetObjectActions())
            {
                if (!ActionListContainsResource(objectAction, resourceId))
                    continue;

                if (TryFindObjectDimensions(objectAction.ObjectId, out int width, out int height))
                    result.Add(new ActionFldDimensions(width, height));
            }

            return result
                .OrderBy(x => x.Width)
                .ThenBy(x => x.Height)
                .ToArray();
        }

        private IReadOnlyList<ActionFldObjectAction> GetObjectActions()
        {
            if (_objectActions is not null)
                return _objectActions;

            var entries = new List<(uint ObjectId, uint ActionOffset)>();
            uint offset = _mappingTableOffset;

            while (CanRead(offset, 8))
            {
                uint objectId = ReadUInt32(offset + 0x00);
                uint actionOffset = ReadUInt32(offset + 0x04);

                if (objectId == ActionListTerminator)
                    break;

                if (actionOffset >= _actionDataOffset && actionOffset < _mappingTableOffset)
                    entries.Add((objectId, actionOffset));

                offset += 8;
            }

            // Compute a hard upper bound for each action block. The 0xFFFFFF01
            // terminator normally appears before it, but the bound keeps malformed FLDs safe.
            var sortedOffsets = entries
                .Select(x => x.ActionOffset)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var result = new List<ActionFldObjectAction>(entries.Count);
            foreach (var entry in entries)
            {
                uint end = _mappingTableOffset;
                foreach (uint candidate in sortedOffsets)
                {
                    if (candidate > entry.ActionOffset)
                    {
                        end = candidate;
                        break;
                    }
                }

                result.Add(new ActionFldObjectAction(entry.ObjectId, entry.ActionOffset, end));
            }

            _objectActions = result;
            return _objectActions;
        }

        private bool ActionListContainsResource(ActionFldObjectAction objectAction, uint resourceId)
        {
            for (uint cursor = objectAction.StartOffset;
                 (long)cursor + 4 <= objectAction.EndOffset;
                 cursor += ActionEntrySize)
            {
                uint entryId = ReadUInt32(cursor);

                if (entryId == ActionListTerminator)
                    return false;

                if (entryId == resourceId)
                    return true;
            }

            return false;
        }

        private bool TryFindObjectDimensions(uint objectId, out int width, out int height)
        {
            width = 0;
            height = 0;

            for (uint offset = _recordsOffset;
                 (long)offset + RecordSize <= _actionDataOffset;
                 offset += RecordSize)
            {
                uint id = ReadUInt32(offset + 0x00);
                uint kind = ReadUInt32(offset + 0x04);

                if (id != objectId || kind != 0)
                    continue;

                uint currentWidth = ReadUInt32(offset + 0x08);
                uint currentHeight = ReadUInt32(offset + 0x0C);

                if (currentWidth == 0 || currentHeight == 0 ||
                    currentWidth > int.MaxValue || currentHeight > int.MaxValue)
                    return false;

                width = (int)currentWidth;
                height = (int)currentHeight;
                return true;
            }

            return false;
        }

        private uint ReadUInt32(long offset)
        {
            if (offset < 0 || offset + 4 > _data.Length)
                throw new EndOfStreamException();

            return BitConverter.ToUInt32(_data, (int)offset);
        }

        private bool CanRead(uint offset, int length)
        {
            return offset <= _data.Length && (long)offset + length <= _data.Length;
        }

        private void ValidateOffset(uint offset, string fieldName, bool allowEnd = false)
        {
            bool invalid = allowEnd ? offset > _data.Length : offset >= _data.Length;
            if (invalid)
                throw new InvalidDataException($"{fieldName} aponta para fora de '{FilePath}'.");
        }

        private string? TryReadNullTerminatedShiftJis(uint offset)
        {
            if (offset >= _data.Length)
                return null;

            int start = (int)offset;
            int end = start;

            while (end < _data.Length && _data[end] != 0)
                end++;

            if (end >= _data.Length)
                return null;

            return ShiftJisEncoding.GetString(_data, start, end - start);
        }

        private static Encoding CreateShiftJisEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(932);
        }

        private static string NormalizeResourcePath(string value)
        {
            return value.Replace('\\', '/').Trim();
        }


        private static IEnumerable<(int Width, int Height)> EnumerateAlignedDimensions(
            uint imageSize,
            int colorDepth)
        {
            if (colorDepth is not (4 or 8))
                yield break;

            long totalBits = checked((long)imageSize * 8);
            if (totalBits % colorDepth != 0)
                yield break;

            long pixelCount = totalBits / colorDepth;
            if (pixelCount <= 0)
                yield break;

            // 999 resources handled by this path are DS bitmap resources and the known
            // dimensions are aligned to 8 pixels. Enumerating width in 8-pixel steps keeps
            // the candidate set small and prevents implausible 1xN factor pairs.
            for (long candidateWidth = 8; candidateWidth * candidateWidth <= pixelCount; candidateWidth += 8)
            {
                if (pixelCount % candidateWidth != 0)
                    continue;

                long candidateHeight = pixelCount / candidateWidth;
                if (candidateHeight < 8 || candidateHeight % 8 != 0)
                    continue;

                if (candidateWidth <= int.MaxValue && candidateHeight <= int.MaxValue)
                {
                    yield return ((int)candidateWidth, (int)candidateHeight);

                    if (candidateWidth != candidateHeight)
                        yield return ((int)candidateHeight, (int)candidateWidth);
                }
            }
        }

        private static bool IsReasonablyCloseToLogical(
            ActionFldDimensions logical,
            int storedWidth,
            int storedHeight)
        {
            // The FLD rectangle may be a display/hit rectangle, so the raw bitmap can be
            // slightly smaller OR larger. Keep the heuristic conservative: each axis must
            // stay within 35% of the FLD dimension, with an 8px minimum tolerance.
            double widthTolerance = Math.Max(8.0, logical.Width * 0.35);
            double heightTolerance = Math.Max(8.0, logical.Height * 0.35);

            return Math.Abs(storedWidth - logical.Width) <= widthTolerance &&
                   Math.Abs(storedHeight - logical.Height) <= heightTolerance;
        }

        private static double DimensionDistanceScore(
            ActionFldDimensions logical,
            int storedWidth,
            int storedHeight)
        {
            // Normalize each axis so large resources do not dominate merely because their
            // absolute pixel difference is larger. Add a small aspect-ratio penalty to make
            // orientation swaps less likely when both factor pairs are otherwise close.
            double widthError = Math.Abs(storedWidth - logical.Width) / (double)Math.Max(1, logical.Width);
            double heightError = Math.Abs(storedHeight - logical.Height) / (double)Math.Max(1, logical.Height);

            double logicalRatio = logical.Width / (double)Math.Max(1, logical.Height);
            double storedRatio = storedWidth / (double)Math.Max(1, storedHeight);
            double ratioError = Math.Abs(Math.Log(storedRatio / logicalRatio));

            return widthError + heightError + (ratioError * 0.20);
        }

        private static bool TryChooseUniqueBestStoredDimension(
            IEnumerable<StoredDimensionCandidate> candidates,
            out StoredDimensionCandidate best)
        {
            best = default;

            var ranked = candidates
                .GroupBy(x => (x.Width, x.Height))
                .Select(g => g.OrderBy(x => x.Score).First())
                .OrderBy(x => x.Score)
                .ToArray();

            if (ranked.Length == 0)
                return false;

            best = ranked[0];

            // Absolute safety limit. A result farther than this is not close enough to the
            // logical rectangle to trust automatically.
            if (best.Score > 0.90)
                return false;

            if (ranked.Length == 1)
                return true;

            // Reject near-ties. This avoids silently choosing between two plausible raw
            // dimensions when the FLD rectangle does not distinguish them well enough.
            double secondScore = ranked[1].Score;
            return secondScore - best.Score >= 0.12;
        }

        private static int Align8(int value)
        {
            return (value + 7) & ~7;
        }

        private static long ImageByteCountFor(int width, int height, int colorDepth)
        {
            return checked((long)width * height * colorDepth / 8);
        }
    }

    internal readonly record struct StoredDimensionCandidate(
        int Width,
        int Height,
        int LogicalWidth,
        int LogicalHeight,
        double Score);

    internal readonly record struct ActionFldObjectAction(
        uint ObjectId,
        uint StartOffset,
        uint EndOffset);

    public readonly record struct ActionFldDimensions(int Width, int Height);

    public sealed record ActionFldResource(
        uint ResourceId,
        string InternalPath,
        int? LogicalWidth,
        int? LogicalHeight);

    public readonly record struct ActionFldResourceInfo(
        string ActionFldPath,
        int LogicalWidth,
        int LogicalHeight);
}
