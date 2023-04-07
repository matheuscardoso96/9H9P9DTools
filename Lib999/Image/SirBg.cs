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

        public SirBg(string arg, bool exportDecomp = false)
        {
            var args = arg.Split('@');
            FileName = Path.GetFileName(args[0]);
            Width = int.Parse(args[1]);
            Height = int.Parse(args[2]);
            ColorDep = int.Parse(args[3]);
            var file = File.ReadAllBytes(args[0]);
            CompleteFile = ATP6.Decode(new MemoryStream(file)).ToArray();
            BinaryReader br = new BinaryReader(new MemoryStream(CompleteFile));
            Header = new SirHeader(br);
            br.BaseStream.Position = Header.Offset0 + 0x18;
            BgInfo = new SirBgInfo(br);
            br.BaseStream.Position = Header.Offset1;
            SirOffsetsArea = new VLQTable(br);
            Image = GetImage(BgInfo, br);
            if (exportDecomp)
            {
                File.WriteAllBytes($"{Path.GetFileNameWithoutExtension(args[0])}_expD.bin", Image);
            }
            Pal = GetPalete(ColorDep, BgInfo , br);
            br.Close();
        }


        private static byte[] GetImage(SirBgInfo sirBgInfo, BinaryReader br) 
        {
            br.BaseStream.Position = sirBgInfo.StartPosition;
            var image = br.ReadBytes((int)sirBgInfo.Size);
            return image;

            /*switch (colorDepth)
            {
                case 4:
                    totalBytes = width * height / 2;
                    break;
                case 8:
                    totalBytes = width * height;
                    break;
                default:
                    break;
            }*/




        }

        private static byte[] GetPalete(int colorDepth, SirBgInfo sirBgInfo, BinaryReader br)
        {
            br.BaseStream.Position = sirBgInfo.PalPosition;

            if (colorDepth == 4)
              return br.ReadBytes(0x20);
            else
                return br.ReadBytes(0x200);
            

        }

        public Bitmap ConvertImageToBmp() 
        {
            switch (ColorDep)
            {
                case 4:
                    return ImageDsConverter.RawIndexedToBitmap(Image, Width, Height, new BGR565(Pal), TileMode.NotTiled, ColorDepth.F4BBP);
                case 8:
                    return ImageDsConverter.RawIndexedToBitmap(Image, Width, Height, new BGR565(Pal), TileMode.NotTiled, ColorDepth.F8BBP);
                default:
                    return null;
            }


        }

        public void InsertImage(string imagePath, string savePath) 
        {
            Bitmap image = new Bitmap(imagePath);
            var convertedImage = Array.Empty<byte>();

            if (image.Width != Width)
                throw new ArgumentException("Invalid image width");
            
            if (image.Height != Height)
                throw new ArgumentException("Invalid image height");


            switch (ColorDep)
            {
                case 4:
                    convertedImage = ImageDsConverter.BitmapToRawIndexed(image, new BGR565(Pal), TileMode.NotTiled, ColorDepth.F4BBP);
                    break;
                case 8:
                    convertedImage = ImageDsConverter.BitmapToRawIndexed(image, new BGR565(Pal), TileMode.NotTiled, ColorDepth.F8BBP);
                    break;
            }
            
            image.Dispose();
            
            if (convertedImage != null)
            {
                var originalFile = new MemoryStream(CompleteFile);
                BinaryWriter bw = new(new MemoryStream(CompleteFile));
                bw.BaseStream.Position = BgInfo.StartPosition;
                bw.Write(convertedImage);
                bw.Close();
                var dest = $"999_converted\\{savePath.Replace(Path.GetFileName(savePath), "")}";
                Directory.CreateDirectory(dest);
                File.WriteAllBytes($"{dest}\\{Path.GetFileName(FileName)}",ATP6.Encode(originalFile.ToArray()));
            }
        }
    }

    public class SirBgInfo 
    {
        public uint StartPosition { get; set; }
        public uint Size { get; set; }
        public uint PalPosition { get; set; }

        public SirBgInfo(BinaryReader br)
        {
            StartPosition = br.ReadUInt32();
            PalPosition = br.ReadUInt32();
            Size = PalPosition - StartPosition;
           
        }
    }
}
