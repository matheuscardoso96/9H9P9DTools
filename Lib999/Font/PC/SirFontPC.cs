using LibDeImagensGbaDs.Conversor;
using LibDeImagensGbaDs.Enums;
using LibDeImagensGbaDs.Paleta;
using System.Drawing;
using System.Text;

namespace Lib999.Font.PC
{
    public class SirFontPC
    {
        public string FontName { get; set; }
        public SirHeaderPC Header { get; set; }
        public List<CharInfoPC> CharInfos { get; set; } = new();
        public FontTablePC FontTable { get; set; }
        public VLQTable VLQArea { get; set; }
        public Encoding JapaneseEncoding { get; set; }

        public SirFontPC(string path)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            JapaneseEncoding = Encoding.GetEncoding(932);

            FontName = Path.GetFileName(path);
            var br = new BinaryReader(File.OpenRead(path));
            Header = new(br);
            br.BaseStream.Position = Header.Offset0;
            FontTable = new(br);
            br.BaseStream.Position = Header.Offset1;
            VLQArea = new(br);
            int count = 0;
            foreach (var charPosition in FontTable.CharInfoOffsetTable)
            {
                br.BaseStream.Position = (long)FontTable.CharInfoStartOffset + (charPosition << 1);
                CharInfos.Add(new(br));
                count++;
            }

            br.Close();

        }

        public SirFontPC(string charInfosDescPath, string fontImagePath, string fontBorderImagePath)
        {
            Header = new(0, 0);
            var charinfosDesc = File.ReadAllLines(charInfosDescPath);
            var fontTableUnknown0 = Convert.ToInt32(charinfosDesc[0].Split('|')[0].Split('=')[1]);
            var fontTableUnknown1 = Convert.ToInt32(charinfosDesc[0].Split('|')[1].Split('=')[1]);
            FontTable = new(charinfosDesc.Length - 1, fontTableUnknown0, fontTableUnknown1);
            FontName = Path.GetFileName(charInfosDescPath).Replace(".txt", "");
            SetCharInfos(charinfosDesc.Skip(1).Take(charinfosDesc.Length - 1).ToArray(), fontImagePath, fontBorderImagePath);

        }

        public void ExportFont(string path)
        {


            var width = 1024;
            var height = CharInfos.Count / 32 * 64;
            var remainder = CharInfos.Count % 16;
            if (remainder > 0)
                height += 64;

            var finalImage = new Bitmap(width, height);

            using (Graphics graph = Graphics.FromImage(finalImage))
            {
                Rectangle ImageSize = new Rectangle(0, 0, width, height);
                graph.FillRectangle(Brushes.Black, ImageSize);
            }

            int x = 0;
            int y = 0;

            var pal = File.ReadAllBytes("graypall.bin");


            using (Graphics g = Graphics.FromImage(finalImage))
            {
                foreach (var charinfo in CharInfos)
                {
                    try
                    {
                        var charImage = ImageDsConverter.RawIndexedToBitmap(charinfo.CharImage, charinfo.Width, charinfo.Height, new BGR565(pal), TileMode.NotTiled, ColorDepth.F8BBP);

                        g.DrawImage(charImage, x, charinfo.YFix < 255 ? y + charinfo.YFix : y);
                        x += 32;
                        if (x == width)
                        {
                            y += 64;
                            x = 0;
                        }

                        //if (x == 128)
                        //{
                        //    break;
                        //}

                        charImage.Dispose();
                    }
                    catch (Exception ex)
                    {

                        throw;
                    }



                }

            }



            var destPath = "999_pc_exported\\" + path.Replace($"{Path.GetFileName(FontName)}", "");

            Directory.CreateDirectory(destPath);

            ExportCharsInfos(destPath);

            finalImage.Save($"{destPath}\\{FontName}.png");

            ExportBorderFont(path);

        }

        private void ExportCharsInfos(string destPath)
        {
            var chars = new List<string>()
            {
            $"TableInfo1={FontTable.Unknown0}|TableInfo2={FontTable.Unknown1}"
           // $"SirUnknown={string.Join(",",VLQArea.DataVLQArea.Select(x => $"0x{x:X2}"))}"
             };

            chars.AddRange(CharInfos.Select(x => $"0x{x.Code:X2}={GetCharFromcode(x.Code)}[]Width={x.Width}[]Height={x.Height}[]WidthBorder={x.WidthBorder}[]HeightBorder={x.HeightBorder}[]XFix={x.XFix}[]YFix={x.YFix}"));
            File.WriteAllLines($"{destPath}\\{FontName}.txt", chars);
        }

        public void ExportBorderFont(string path)
        {


            var width = 1024;
            var height = CharInfos.Count / 32 * 64;
            var remainder = CharInfos.Count % 16;
            if (remainder > 0)
                height += 64;

            var finalImage = new Bitmap(width, height);

            using (Graphics graph = Graphics.FromImage(finalImage))
            {
                Rectangle ImageSize = new Rectangle(0, 0, width, height);
                graph.FillRectangle(Brushes.Black, ImageSize);
            }

            int x = 0;
            int y = 0;

            var pal = File.ReadAllBytes("graypall.bin");

            using (Graphics g = Graphics.FromImage(finalImage))
            {
                foreach (var charinfo in CharInfos)
                {
                    var charImage = ImageDsConverter.RawIndexedToBitmap(charinfo.CharBorderImage, charinfo.WidthBorder, charinfo.HeightBorder, new BGR565(pal), TileMode.NotTiled, ColorDepth.F8BBP);
                    g.DrawImage(charImage, x, charinfo.YFix < 255 ? y + charinfo.YFix : y);
                    x += 32;
                    if (x == width)
                    {
                        y += 64;
                        x = 0;
                    }

                    //if (x == 128)
                    //{
                    //    break;
                    //}

                    charImage.Dispose();


                }

            }


            var destPath = "999_pc_exported\\" + path.Replace($"{Path.GetFileName(FontName)}", "");

            finalImage.Save($"{destPath}\\{FontName}_border.png");

        }

        string GetCharFromcode(ushort code)
        {

            if (code >= 0x81)
            {
                var bytes = BitConverter.GetBytes(code).Take(2).Reverse().ToArray();
                var tex = JapaneseEncoding.GetString(bytes);
                return tex;
            }
            else
            {
                return $"{Convert.ToChar(code)}";
            }

        }

        private void SetCharInfos(string[] charinfosDesc, string fontImagePath, string fontBorderImagePath)
        {

            var pal = File.ReadAllBytes("graypall.bin");
            var fontImage = new Bitmap(fontImagePath);
            var fontImageBorder = new Bitmap(fontBorderImagePath);
            CharInfos = new List<CharInfoPC>();

            int x = 0;
            int y = 0;
            Directory.CreateDirectory("_chars_font_pc");

            int ocunt = 0;

            foreach (var charinfoDesc in charinfosDesc)
            {
                var charInfo = new CharInfoPC(charinfoDesc);
                //var rectangleFont = new Rectangle(x, charInfo.YFix < 255 ? y + charInfo.YFix : y, charInfo.Width, charInfo.Height);
                //var rectangleBorder = new Rectangle(x, charInfo.YFix < 255 ? y + charInfo.YFix : y, charInfo.WidthBorder, charInfo.HeightBorder);
                var rectangleFont = new Rectangle(x, y, charInfo.Width, charInfo.Height);
                var rectangleBorder = new Rectangle(x, y, charInfo.WidthBorder, charInfo.HeightBorder);
                var charImage = fontImage.Clone(rectangleFont, fontImage.PixelFormat);
                var charBorderImage = fontImageBorder.Clone(rectangleBorder, fontImage.PixelFormat);
                //charImage.Save($"_chars_font_pc\\{ocunt}.png");
                // charBorderImage.Save($"_chars_font_pc\\{ocunt}_b.png");
                var chImage = ImageDsConverter.BitmapToRawIndexed(charImage, new BGR565(pal), TileMode.NotTiled, ColorDepth.F8BBP);

                var chBorderImage = ImageDsConverter.BitmapToRawIndexed(charBorderImage, new BGR565(pal), TileMode.NotTiled, ColorDepth.F8BBP);
                charInfo.SetCharImage(chImage, chBorderImage);
                CharInfos.Add(charInfo);

                x += 32;
                if (x == fontImage.Width)
                {
                    y += 64;
                    x = 0;
                }

                if (y == fontImage.Height)
                    break;

                ocunt++;
            }


        }

        public void SaveSirFont(string path)
        {
            var sirfontFile = new MemoryStream();

            using BinaryWriter bw = new(sirfontFile);
            bw.BaseStream.Position = 0x14;
            CharInfos = CharInfos.OrderBy(x => x.Code).ToList();
            foreach (var charInfo in CharInfos)
            {
                FontTable.AddCharInfoOffset((int)(bw.BaseStream.Position - 0x14));
                charInfo.WriteCharInfo(bw);
                bw.AlignBy(4);
            }

            bw.AlignBy(4);
            Header.SetOffset0(bw.BaseStream.Position);
            FontTable.WriteSubHeader(bw);
            var charInfoStartOffsetPosition = (uint)bw.BaseStream.Position - 0x14;
            FontTable.WriteTable(bw);
            bw.AlignBy(16);
            Header.SetOffset1((int)bw.BaseStream.Position);
            VLQArea = new VLQTable();
            VLQArea.DecompressedValues.Add(4);
            VLQArea.DecompressedValues.Add(8);
            VLQArea.DecompressedValues.Add(charInfoStartOffsetPosition);
            VLQArea.ConvertToDataVLQ();
            VLQArea.WriteDataVLQ(bw);
            bw.Write((byte)0);
            bw.AlignBy(16);
            bw.BaseStream.Position = 0;
            Header.Write(bw);
            var dest = $"999_pc_converted\\{path.Replace(Path.GetFileName(path), "")}";
            Directory.CreateDirectory(dest);
            File.WriteAllBytes($"{dest}\\{FontName}", sirfontFile.ToArray());
        }

    }
}
