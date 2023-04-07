namespace Lib999.Text
{
    public class SirSubTableV6
    {
        public uint Title1Offset { get; set; }
        public uint SubTableOffset { get; set; }
        public List<uint> SubTable { get; set; } = new();
        public List<uint> SubTableArgs0 { get; set; } = new();

        public SirSubTableV6(BinaryReader br)
        {
            Title1Offset = br.ReadUInt32();
            SubTableOffset = br.ReadUInt32();
        }

        public void GetSubTable(BinaryReader br)
        {
            br.BaseStream.Position = SubTableOffset;

            while (true)
            {
                var offset = br.ReadUInt32();
                if (offset == 0)
                    break;

                SubTable.Add(offset);
                SubTable.Add(br.ReadUInt32());
                SubTable.Add(br.ReadUInt32());
                SubTableArgs0.Add(br.ReadUInt32());
                SubTableArgs0.Add(br.ReadUInt32());
                SubTableArgs0.Add(br.ReadUInt32());

            }

        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Title1Offset);
            bw.Write(SubTableOffset);
        }

        public void WriteSubTable(BinaryWriter bw)
        {
            int args0Count = 0;

            for (int i = 0; i < SubTable.Count; i += 3)
            {
                bw.Write(SubTable[i]);
                bw.Write(SubTable[i + 1]);
                bw.Write(SubTable[i + 2]);
                bw.Write(SubTableArgs0[args0Count]);
                bw.Write(SubTableArgs0[args0Count + 1]);
                bw.Write(SubTableArgs0[args0Count + 2]);
                args0Count += 3;
            }

            bw.Write(0);
        }

    }
}
