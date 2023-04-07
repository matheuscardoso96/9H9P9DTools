namespace Lib999.Text
{
    public class SirSubTableV1
    {
        public uint Title1Offset { get; set; }
        public uint Title2Offset { get; set; }
        public uint Title3Offset { get; set; }
        public uint SubTableOffset { get; set; }
        public List<uint> SubTable { get; set; } = new();

        public SirSubTableV1(BinaryReader br)
        {
            Title1Offset = br.ReadUInt32();
            Title2Offset = br.ReadUInt32();
            Title3Offset = br.ReadUInt32();
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
            bw.Write(Title2Offset);
            bw.Write(Title3Offset);
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
