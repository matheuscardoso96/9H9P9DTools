namespace Lib999.Font.PC
{
    public class CharInfoPC
    {

        public ushort Code { get; set; }
        public short XFix { get; set; }
        public short YFix { get; set; }
        public int Width { get; set; }
        public byte Height { get; set; }
        public int WidthBorder { get; set; }
        public byte HeightBorder { get; set; }
        public byte[] CharImage { get; set; }
        public byte[] CharBorderImage { get; set; }
        public CharInfoPC(BinaryReader br)
        {
            Code = br.ReadUInt16();
            XFix = br.ReadInt16();
            YFix = br.ReadInt16();
            Width = br.ReadByte();
            Height = br.ReadByte();
            WidthBorder = br.ReadByte();
            HeightBorder = br.ReadByte();
            CharImage = br.ReadBytes(Width * Height);
            CharBorderImage = br.ReadBytes(WidthBorder * HeightBorder);
        }
        public CharInfoPC(string info)
        {
            var infos = info.Split(new string[] { "[]" }, StringSplitOptions.RemoveEmptyEntries); ;
            Code = Convert.ToUInt16(infos[0].Split('=')[0], 16);
            Width = Convert.ToByte(infos[1].Split('=')[1]);
            Height = Convert.ToByte(infos[2].Split('=')[1]);
            WidthBorder = Convert.ToByte(infos[3].Split('=')[1]);
            HeightBorder = Convert.ToByte(infos[4].Split('=')[1]);
            XFix = Convert.ToByte(infos[5].Split('=')[1]);
            YFix = Convert.ToByte(infos[6].Split('=')[1]);
            CharImage = Array.Empty<byte>();
            CharBorderImage = Array.Empty<byte>();

        }

        public void SetCharImage(byte[] chImage, byte[] chBorderImage)
        {
            CharImage = chImage;
            CharBorderImage = chBorderImage;
        }

        public void WriteCharInfo(BinaryWriter bw)
        {
            if (CharImage != null)
            {
                bw.Write(Code);
                bw.Write(XFix);
                bw.Write(YFix);
                bw.Write((byte)Width);
                bw.Write(Height);
                bw.Write((byte)WidthBorder);
                bw.Write(HeightBorder);
                bw.Write(CharImage);
                bw.Write(CharBorderImage);
            }
        }
    }
}
