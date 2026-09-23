using Lib999.Compression;
using LibDeImagensGbaDs.Conversor;
using LibDeImagensGbaDs.Enums;
using LibDeImagensGbaDs.Paleta;
using System.Drawing;

namespace Lib999.Image
{
    public class SirBg
    {
        public SirHeader Header { get; set; }
        public VLQTable SirOffsetsArea { get; set; }
        public SirBgInfo BgInfo { get; set; }
        public byte[] Image { get; set; }
        public byte[] Pal { get; set; }
        public string FileName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ColorDep { get; set; }
        public byte[] CompleteFile { get; set; }
        public string? LastConversionError { get; private set; }

        public SirBg(string arg, bool exportDecomp = false)
        {
            var args = arg.Split('@');

            var sourcePath = args[0];

            if (args.Length < 4)
            {
                sourcePath = arg;
            }

            FileName = Path.GetFileName(sourcePath);

            var file = File.ReadAllBytes(sourcePath);
            CompleteFile = ATP6.Decode(new MemoryStream(file)).ToArray();

            using BinaryReader br = new BinaryReader(new MemoryStream(CompleteFile));
            Header = new SirHeader(br);
            // The BG SIR0 root contains the raw bitmap geometry before the three
            // data pointers. Read the complete root instead of jumping directly to +0x18.
            br.BaseStream.Position = Header.Offset0;
            BgInfo = new SirBgInfo(br);
            br.BaseStream.Position = Header.Offset1;
            SirOffsetsArea = new VLQTable(br);
            Image = GetImage(BgInfo, br);

            // Retrocompatibilidade: se a entrada ainda for path@width@height@bpp,
            // os valores explícitos continuam tendo prioridade.
            if (args.Length >= 4 &&
                int.TryParse(args[1], out int explicitWidth) &&
                int.TryParse(args[2], out int explicitHeight) &&
                int.TryParse(args[3], out int explicitColorDepth))
            {
                Width = explicitWidth;
                Height = explicitHeight;
                ColorDep = explicitColorDepth;
            }
            else
            {
                ResolveMetadata(sourcePath);
            }

            if (exportDecomp)
            {
                File.WriteAllBytes($"{Path.GetFileNameWithoutExtension(sourcePath)}_expD.bin", Image);
            }

            Pal = GetPalete(ColorDep, BgInfo, br);
        }

        private void ResolveMetadata(string sourcePath)
        {
            int? paletteColorDepth = TryInferColorDepthFromPalette(BgInfo);

            // PRIORIDADE 1: action.fld.
            //
            // O FLD define quais resources pertencem ao objeto e fornece a geometria
            // lógica usada pelo minigame. Primeiro tentamos resolver a dimensão física
            // a partir do objeto que referencia ESTE resource, validando o resultado
            // contra o tamanho real do bloco gráfico e o BPP inferido pela paleta.
            ActionFld? fld = null;
            ActionFld.TryOpenForDat(sourcePath, out fld);
            string resourceFileName = Path.GetFileName(sourcePath);

            if (fld is not null)
            {
                var resourceMatches = new HashSet<(int Width, int Height, int Depth)>();

                foreach (int depth in CandidateColorDepths(paletteColorDepth))
                {
                    if (fld.TryInferStoredDimensionsForResource(
                        resourceFileName,
                        BgInfo.Size,
                        depth,
                        out int width,
                        out int height))
                    {
                        resourceMatches.Add((width, height, depth));
                    }
                }

                if (resourceMatches.Count == 1)
                {
                    var match = resourceMatches.First();
                    Width = match.Width;
                    Height = match.Height;
                    ColorDep = match.Depth;
                    return;
                }
            }

            // PRIORIDADE 2: geometria nativa do DAT/SIR0.
            //
            // Alguns objetos do FLD representam o retângulo lógico/visual e não a
            // dimensão exata do bitmap. Nesses casos, usamos os campos de geometria
            // presentes no próprio SIR0 somente como fallback. O resultado só é aceito
            // quando fecha EXATAMENTE com o tamanho do bloco gráfico.
            foreach (int depth in CandidateColorDepths(paletteColorDepth))
            {
                if (BgInfo.TryGetStoredDimensions(depth, out int sirWidth, out int sirHeight))
                {
                    Width = sirWidth;
                    Height = sirHeight;
                    ColorDep = depth;
                    return;
                }
            }

            // Fallback adicional conhecido: BG de tela inteira do Nintendo DS.
            foreach (int depth in CandidateColorDepths(paletteColorDepth))
            {
                if (ImageByteCountFor(256, 192, depth) == BgInfo.Size)
                {
                    Width = 256;
                    Height = 192;
                    ColorDep = depth;
                    return;
                }
            }

            // Último fallback automático do FLD: se não conseguimos ligar o resource
            // a um objeto específico, varremos as dimensões dos objetos e só aceitamos
            // quando existe um único resultado compatível com o tamanho do bitmap.
            if (fld is not null)
            {
                var matches = new HashSet<(int Width, int Height, int Depth)>();

                foreach (int depth in CandidateColorDepths(paletteColorDepth))
                {
                    if (fld.TryInferStoredDimensions(BgInfo.Size, depth, out int width, out int height))
                        matches.Add((width, height, depth));
                }

                if (matches.Count == 1)
                {
                    var match = matches.First();
                    Width = match.Width;
                    Height = match.Height;
                    ColorDep = match.Depth;
                    return;
                }
            }

            string fldCandidates = fld is null
                ? "action.fld não encontrado"
                : fld.DescribeResourceDimensionCandidates(resourceFileName);

            throw new InvalidDataException(
                $"Não foi possível descobrir automaticamente resolução/BPP de '{sourcePath}' " +
                $"(bloco gráfico: 0x{BgInfo.Size:X} bytes, paleta: 0x{BgInfo.PaletteSize:X} bytes; " +
                $"candidatos do FLD: {fldCandidates}). " +
                "Se este arquivo já estiver na fileExportList, mantenha @largura@altura@bpp para ele.");
        }

        private static IEnumerable<int> CandidateColorDepths(int? preferred)
        {
            if (preferred is 4 or 8)
            {
                yield return preferred.Value;
                yield break;
            }

            yield return 4;
            yield return 8;
        }

        private static int? TryInferColorDepthFromPalette(SirBgInfo sirBgInfo)
        {
            return sirBgInfo.PaletteSize switch
            {
                0x20 => 4,
                0x200 => 8,
                _ => null
            };
        }

        private static bool TryInferColorDepthFromExactSize(
            int width,
            int height,
            uint imageSize,
            out int colorDepth)
        {
            colorDepth = 0;
            long pixels = checked((long)width * height);
            if (pixels <= 0)
                return false;

            long totalBits = checked((long)imageSize * 8);
            if (totalBits % pixels != 0)
                return false;

            long depth = totalBits / pixels;
            if (depth is not (4 or 8))
                return false;

            colorDepth = (int)depth;
            return true;
        }

        private static int Align8(int value)
        {
            return (value + 7) & ~7;
        }

        private static long ImageByteCountFor(int width, int height, int colorDepth)
        {
            return checked((long)width * height * colorDepth / 8);
        }

        private static byte[] GetImage(SirBgInfo sirBgInfo, BinaryReader br)
        {
            br.BaseStream.Position = sirBgInfo.StartPosition;
            return br.ReadBytes((int)sirBgInfo.Size);
        }

        private static byte[] GetPalete(int colorDepth, SirBgInfo sirBgInfo, BinaryReader br)
        {
            br.BaseStream.Position = sirBgInfo.PalPosition;

            int expectedPaletteSize = colorDepth == 4 ? 0x20 : 0x200;

            if (sirBgInfo.PaletteSize == expectedPaletteSize)
                return br.ReadBytes((int)sirBgInfo.PaletteSize);

            return br.ReadBytes(expectedPaletteSize);
        }

        public Bitmap ConvertImageToBmp()
        {
            return ColorDep switch
            {
                4 => ImageDsConverter.RawIndexedToBitmap(
                    Image, Width, Height, new BGR565(Pal), TileMode.NotTiled, ColorDepth.F4BBP),
                8 => ImageDsConverter.RawIndexedToBitmap(
                    Image, Width, Height, new BGR565(Pal), TileMode.NotTiled, ColorDepth.F8BBP),
                _ => throw new InvalidDataException($"Color depth inválido: {ColorDep}")
            };
        }

        public bool PngToSirBg(string imagePath, string savePath)
        {
            LastConversionError = null;
            using Bitmap image = new Bitmap(imagePath);
            byte[] convertedImage;

            if (image.Width != Width)
                throw new ArgumentException("Invalid image width");

            if (image.Height != Height)
                throw new ArgumentException("Invalid image height");

            convertedImage = ColorDep switch
            {
                4 => ImageDsConverter.BitmapToRawIndexed(
                    image, new BGR565(Pal), TileMode.NotTiled, ColorDepth.F4BBP),
                8 => ImageDsConverter.BitmapToRawIndexed(
                    image, new BGR565(Pal), TileMode.NotTiled, ColorDepth.F8BBP),
                _ => throw new InvalidDataException($"Color depth inválido: {ColorDep}")
            };

            if (convertedImage.Length != BgInfo.Size)
            {
                LastConversionError =
                    $"O PNG convertido gerou 0x{convertedImage.Length:X} bytes, " +
                    $"mas o bloco gráfico original possui 0x{BgInfo.Size:X} bytes.";

                return false;
            }

            var originalFile = new MemoryStream(CompleteFile);
            using (BinaryWriter bw = new(new MemoryStream(CompleteFile)))
            {
                bw.BaseStream.Position = BgInfo.StartPosition;
                bw.Write(convertedImage);
            }

            // Entradas manuais da fileExportList podem chegar como:
            // c_999\...\arquivo.dat@256@192@8
            // O sufixo @W@H@BPP é metadado da tool e NÃO faz parte do nome do arquivo.
            int metadataIndex = savePath.IndexOf('@');
            if (metadataIndex >= 0)
                savePath = savePath[..metadataIndex];

            // O scan automático trabalha com paths absolutos.
            // Para 999_converted precisamos voltar ao path relativo do projeto
            // (ex.: c_999\root\bg\...), mantendo c_999 porque ele é removido
            // depois pelo fluxo de substituição do Program.cs.
            savePath = MakeWorkingDirectoryRelative(savePath);

            string? saveDirectory = Path.GetDirectoryName(savePath);
            string dest = string.IsNullOrEmpty(saveDirectory)
                ? "999_converted"
                : Path.Combine("999_converted", saveDirectory);

            Directory.CreateDirectory(dest);
            File.WriteAllBytes(
                Path.Combine(dest, Path.GetFileName(savePath)),
                ATP6.Encode(originalFile.ToArray()));

            return true;
        }

        public void SirBgToPng(string destination)
        {
            using var img = ConvertImageToBmp();

            // O scan automático pode chamar esta função com path absoluto
            // (C:\...\c_999\root\...). Converta primeiro para relativo.
            destination = MakeWorkingDirectoryRelative(destination);

            // c_999 é a cópia original. Na exportação o destino continua sendo
            // 999_exported\999\root\..., portanto removemos somente o prefixo "c_".
            if (destination.StartsWith("c_", StringComparison.OrdinalIgnoreCase))
                destination = destination[2..];

            string? destinationDirectory = Path.GetDirectoryName(destination);
            string dest = string.IsNullOrEmpty(destinationDirectory)
                ? "999_exported"
                : Path.Combine("999_exported", destinationDirectory);

            Directory.CreateDirectory(dest);
            img.Save(Path.Combine(dest, $"{FileName}.png"));
        }

        /// <summary>
        /// Converte um path absoluto que esteja dentro do diretório de trabalho
        /// em path relativo. Paths relativos antigos continuam inalterados.
        ///
        /// Isso é necessário porque o scanner de minigames usa Path.GetFullPath()
        /// internamente, enquanto 999_exported/999_converted precisam preservar
        /// apenas c_999\root\... ou 999\root\....
        /// </summary>
        private static string MakeWorkingDirectoryRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            string normalized = path
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (!Path.IsPathRooted(normalized))
                return normalized;

            string workingDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
            string fullPath = Path.GetFullPath(normalized);
            string relative = Path.GetRelativePath(workingDirectory, fullPath);

            // Os arquivos esperados pela tool ficam dentro do diretório de trabalho.
            // Se algum caller fornecer um path externo, não monte algo como
            // 999_exported\C:\... nem permita escapar com "..".
            if (relative == ".." ||
                relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"O arquivo '{path}' está fora do diretório de trabalho '{workingDirectory}'.");
            }

            return relative;
        }
    }

    public class SirBgInfo
    {
        // Os quatro primeiros dwords do root do BG são os limites do retângulo
        // da textura em unidades de tiles 8x8, e NÃO width/height-1.
        //
        // Exemplo real (a31_helm/resource/bg2_rudder.dat):
        //   left=4, top=0, right=27, bottom=23
        //   width  = (27 - 4 + 1) * 8 = 192
        //   height = (23 - 0 + 1) * 8 = 192
        public uint TileLeft { get; set; }
        public uint TileTop { get; set; }
        public uint TileRight { get; set; }
        public uint TileBottom { get; set; }

        public uint Unknown10 { get; set; }
        public uint Unknown14 { get; set; }

        public uint StartPosition { get; set; }
        public uint Size { get; set; }
        public uint PalPosition { get; set; }
        public uint EndPosition { get; set; }
        public uint PaletteSize { get; set; }

        public SirBgInfo(BinaryReader br)
        {
            long rootPosition = br.BaseStream.Position;

            if (rootPosition < 0 || rootPosition + 0x24 > br.BaseStream.Length)
                throw new InvalidDataException("Root de BG incompleto no SIR0.");

            TileLeft = br.ReadUInt32();
            TileTop = br.ReadUInt32();
            TileRight = br.ReadUInt32();
            TileBottom = br.ReadUInt32();
            Unknown10 = br.ReadUInt32();
            Unknown14 = br.ReadUInt32();
            StartPosition = br.ReadUInt32();
            PalPosition = br.ReadUInt32();
            EndPosition = br.ReadUInt32();

            if (StartPosition >= br.BaseStream.Length)
                throw new InvalidDataException("StartPosition aponta para fora do SIR0 de BG.");

            if (PalPosition < StartPosition || PalPosition > br.BaseStream.Length)
                throw new InvalidDataException("PalPosition inválido no SIR0 de BG.");

            Size = PalPosition - StartPosition;

            // EndPosition is useful for automatic BPP detection, but keep old files
            // compatible if a different/unknown variant does not expose a valid end pointer.
            if (EndPosition >= PalPosition && EndPosition <= br.BaseStream.Length)
                PaletteSize = EndPosition - PalPosition;
            else
                PaletteSize = 0;
        }

        public bool TryGetStoredDimensions(int colorDepth, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (colorDepth is not (4 or 8))
                return false;

            // Os valores são inclusive bounds em tiles: left/top/right/bottom.
            // O patch anterior tratava right/bottom como width/height-1 e ignorava
            // left/top; isso quebra recursos recortados/posicionados dentro do canvas.
            if (TileRight < TileLeft || TileBottom < TileTop)
                return false;

            // Limite de segurança para rejeitar outro tipo de SIR0/corrupção.
            if (TileLeft > 0x3FF || TileTop > 0x3FF ||
                TileRight > 0x3FF || TileBottom > 0x3FF)
                return false;

            long tileWidth = (long)TileRight - TileLeft + 1;
            long tileHeight = (long)TileBottom - TileTop + 1;
            long candidateWidth = checked(tileWidth * 8);
            long candidateHeight = checked(tileHeight * 8);

            if (candidateWidth <= 0 || candidateHeight <= 0 ||
                candidateWidth > int.MaxValue || candidateHeight > int.MaxValue)
                return false;

            long expectedSize = checked(candidateWidth * candidateHeight * colorDepth / 8);
            if (expectedSize != Size)
                return false;

            width = (int)candidateWidth;
            height = (int)candidateHeight;
            return true;
        }
    }
}
