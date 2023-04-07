namespace Lib999.Text
{
    public class SirSubTableV4
    {
        public uint Title1Offset { get; set; }
        public List<uint> UnknownBytes { get; set; } = new();
        public uint SubTableOffset { get; set; }
        public List<uint> SubTable { get; set; } = new();

        public SirSubTableV4(BinaryReader br)
        {
            Title1Offset = br.ReadUInt32();
            for (int i = 0; i < 4; i++)
                UnknownBytes.Add(br.ReadUInt32());

            SubTableOffset = br.ReadUInt32();
        }

        public void GetSubTable(BinaryReader br)
        {
            br.BaseStream.Position = SubTableOffset;
            while (true)
            {
                var offset = (uint)br.ReadUInt32();
                if (offset == 0)
                    break;

                SubTable.Add(offset);

            }

        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Title1Offset);
            UnknownBytes.ForEach(x => bw.Write(x));
            bw.Write(SubTableOffset);
        }

        public void WriteSubTable(BinaryWriter bw)
        {
            foreach (var item in SubTable)
                bw.Write(item);

            bw.Write(0);
        }
    
    }
}
