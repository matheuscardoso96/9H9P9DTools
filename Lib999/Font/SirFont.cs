using LibDeImagensGbaDs.Conversor;
using LibDeImagensGbaDs.Enums;
using LibDeImagensGbaDs.Paleta;
using System.Drawing;
using System.Text;

namespace Lib999.Font
{
    public class SirFont
    {
        public string FontName { get; set; }
        public SirHeader Header { get; set; }
        public List<CharInfo> CharInfos { get; set; } = new();
        public FontTable FontTable { get; set; }
        public VLQTable VLQArea { get; set; }
        public Encoding JapaneseEncoding { get; set; }

        public SirFont(string path)
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
                br.BaseStream.Position = (charPosition << 1) + FontTable.CharInfoStartOffset;
                CharInfos.Add(new CharInfo(br));
                count++;
            }

            br.Close();

        }

        public SirFont(string charInfosDescPath, string fontImagePath)
        {
            Header = new SirHeader(0,0);
            var charinfosDesc = File.ReadAllLines(charInfosDescPath);
            var fontTableUnknown0 = Convert.ToInt32(charinfosDesc[0].Split('|')[0].Split('=')[1]);
            var fontTableUnknown1 = Convert.ToInt32(charinfosDesc[0].Split('|')[1].Split('=')[1]);
            FontTable = new FontTable(charinfosDesc.Length - 1, fontTableUnknown0, fontTableUnknown1);
            FontName = Path.GetFileName(charInfosDescPath).Replace(".txt","");
            SetCharInfos(charinfosDesc.Skip(1).Take(charinfosDesc.Length -1).ToArray(), fontImagePath);
           
        }

        public void ExportFont(string path)
        {
            

            var width = 256;
            var height = (CharInfos.Count / 16) * 16;
            var remainder = CharInfos.Count % 24;
            if (remainder > 0)
                height += 16;

            var finalImage = new Bitmap(width, height);

            using (Graphics graph = Graphics.FromImage(finalImage))
            {
                Rectangle ImageSize = new Rectangle(0, 0, width, height);
                graph.FillRectangle(Brushes.Black, ImageSize);
            }

            int x = 0;
            int y = 0;

            using (Graphics g = Graphics.FromImage(finalImage))
            {
                foreach (var charinfo in CharInfos)
                {
                    var charImage = ImageDsConverter.RawIndexedToBitmap(charinfo.CharImage, charinfo.Width, charinfo.Height, new BGR565(), TileMode.NotTiled, ColorDepth.F1BBP);
                    g.DrawImage(charImage, x, charinfo.YFix < 255? y + charinfo.YFix: y);
                    x += 16;
                    if (x == width)
                    {
                        y += 16;
                        x = 0;
                    }


                    charImage.Dispose();
                }

            }

            var chars = new List<string>()
            {
            $"TableInfo1={FontTable.Unknown0}|TableInfo2={FontTable.Unknown1}"
           // $"SirUnknown={string.Join(",",VLQArea.DataVLQArea.Select(x => $"0x{x:X2}"))}"
             };

            var destPath = "999_exported\\" + path.Replace($"{Path.GetFileName(FontName)}","");
            
            Directory.CreateDirectory(destPath);

            chars.AddRange(CharInfos.Select(x => $"0x{x.Code:X2}={GetCharFromcode(x.Code)}[]Height={x.Height}[]GlyphWidth={x.GlyphWidth}[]XFix={x.XFix}[]YFix={x.YFix}[]Unk0={x.Unknown0}[]Unk1={x.Unknown1}"));
            File.WriteAllLines($"{destPath}\\{FontName}.txt", chars);

            finalImage.Save($"{destPath}\\{FontName}.png");
            
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

        private void SetCharInfos(string[] charinfosDesc, string fontImagePath)
        {
            
           
            var fontImage = new Bitmap(fontImagePath);
            CharInfos = new List<CharInfo>();

            int x = 0;
            int y = 0;

            foreach (var charinfoDesc in charinfosDesc)
            {
                var charInfo = new CharInfo(charinfoDesc);
                var rectangle = new Rectangle(x, charInfo.YFix < 255?  y + charInfo.YFix: y, 16, charInfo.Height);
                var charImage = fontImage.Clone(rectangle, fontImage.PixelFormat);
                var chImage = ImageDsConverter.BitmapToRawIndexed(charImage, new BGR565(), TileMode.NotTiled, ColorDepth.F1BBP);
                charInfo.SetCharImage(chImage);
                CharInfos.Add(charInfo);

                x += 16;
                if (x == fontImage.Width)
                {
                    y += 16;
                    x = 0;
                }

                if (y == fontImage.Height)
                    break;

               
            }


        }

        public void SaveSirFont(string path) 
        {
            var sirfontFile = new MemoryStream();

            using BinaryWriter bw = new(sirfontFile);
            bw.BaseStream.Position = 0x10;
            CharInfos = CharInfos.OrderBy(x => x.Code).ToList();
            foreach (var charInfo in CharInfos)
            {
                FontTable.AddCharInfoOffset((int)(bw.BaseStream.Position - 0x10));
                charInfo.WriteCharInfo(bw);
            }

            bw.AlignBy(4);
            Header.SetOffset0((int)bw.BaseStream.Position);
            FontTable.WriteSubHeader(bw);
            var charInfoStartOffsetPosition = (uint)bw.BaseStream.Position - 12;
            FontTable.WriteTable(bw);
            bw.AlignBy(16);
            Header.SetOffset1((int)bw.BaseStream.Position);
            VLQArea = new VLQTable();
            VLQArea.DecompressedValues.Add(4);
            VLQArea.DecompressedValues.Add(4);
            VLQArea.DecompressedValues.Add(charInfoStartOffsetPosition);
            VLQArea.ConvertToDataVLQ();
            VLQArea.WriteDataVLQ(bw);
            bw.Write((byte)0);
            bw.AlignBy(16);
            bw.BaseStream.Position = 0;
            Header.Write(bw);
            var dest = $"999_converted\\{path.Replace(Path.GetFileName(path),"")}" ;
            Directory.CreateDirectory(dest);
            File.WriteAllBytes($"{dest}\\{FontName}", sirfontFile.ToArray());
        }

    }
}
