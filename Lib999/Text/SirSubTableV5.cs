namespace Lib999.Text
{
    public class SirSubTableV5
    {
        public uint Title1Offset { get; set; }
        public uint SubTableOffset { get; set; }
        public List<uint> SubTable { get; set; } = new();
        public List<uint> SubTableArgs0 { get; set; } = new();
        public List<uint> SubTableArgs1 { get; set; } = new();

        public SirSubTableV5(BinaryReader br)
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
                SubTableArgs0.Add(br.ReadUInt32());
                SubTable.Add(br.ReadUInt32());
                SubTable.Add(br.ReadUInt32());
                SubTableArgs1.Add(br.ReadUInt32());
                SubTableArgs1.Add(br.ReadUInt32());
                SubTableArgs1.Add(br.ReadUInt32());
                SubTableArgs1.Add(br.ReadUInt32());

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
            int subTableArgs1 = 0;

            for (int i = 0; i < SubTable.Count; i+=3)
            {
                bw.Write(SubTable[i]);
                bw.Write(SubTableArgs0[args0Count]);
                bw.Write(SubTable[i + 1]);
                bw.Write(SubTable[i + 2]);
                bw.Write(SubTableArgs1[subTableArgs1]);
                bw.Write(SubTableArgs1[subTableArgs1 + 1]);
                bw.Write(SubTableArgs1[subTableArgs1 + 2]);
                bw.Write(SubTableArgs1[subTableArgs1 + 3]);
                args0Count++;
                subTableArgs1 += 4;
            }

            bw.Write(0);
        }

    }

}
