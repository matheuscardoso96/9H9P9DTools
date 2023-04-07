namespace Lib999.Font
{
    public class CharInfo 
    {
        public int Width { get; set; } = 16;
        public ushort Code { get; set; }
        public byte XFix { get; set; }
        public byte YFix { get; set; }
        public byte Unknown0 { get; set; }
        public byte Height { get; set; }
        public byte GlyphWidth { get; set; }
        public byte Unknown1 { get; set; }
        public byte[] CharImage { get; set; }
        public CharInfo(BinaryReader br)
        {
            Code = br.ReadUInt16();
            XFix = br.ReadByte();
            YFix = br.ReadByte();
 
            Unknown0 = br.ReadByte();
            Height = br.ReadByte();
            GlyphWidth = br.ReadByte();
            Unknown1 = br.ReadByte();
            CharImage = br.ReadBytes(Height * 2);
        }
        public CharInfo(string info)
        {
            var infos = info.Split(new string[] { "[]" }, StringSplitOptions.RemoveEmptyEntries); ;
            Code = Convert.ToUInt16(infos[0].Split('=')[0],16);
            Height = Convert.ToByte(infos[1].Split('=')[1]);
            GlyphWidth = Convert.ToByte(infos[2].Split('=')[1]);
            XFix = Convert.ToByte(infos[3].Split('=')[1]);
            YFix = Convert.ToByte(infos[4].Split('=')[1]);
            Unknown0 = Convert.ToByte(infos[5].Split('=')[1]);
            Unknown1 = Convert.ToByte(infos[6].Split('=')[1]);
            CharImage = Array.Empty<byte>();

        }

        public void SetCharImage(byte[] chImage) 
        {
            CharImage = chImage;
        }

        public void WriteCharInfo(BinaryWriter bw) 
        {
            if (CharImage != null)
            {
                bw.Write(Code);
                bw.Write(XFix);
                bw.Write(YFix);
                bw.Write(Unknown0);
                bw.Write(Height);
                bw.Write(GlyphWidth);
                bw.Write(Unknown1);
                bw.Write(CharImage);
            }
        }
    }
}
