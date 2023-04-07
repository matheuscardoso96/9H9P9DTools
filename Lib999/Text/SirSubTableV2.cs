namespace Lib999.Text
{
    public class SirSubTableV2
    {
        public uint Title1Offset { get; set; }
        public uint SubTableOffset { get; set; }
        public List<uint> SubTable { get; set; } = new();

        public SirSubTableV2(BinaryReader br)
        {
            Title1Offset = br.ReadUInt32();
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
    }

    public class SirSubTablePCV1
    {
        public long TableOffset { get; set; }
        public List<ulong> Table { get; set; } = new();

        public SirSubTablePCV1(long tableOffset)
        {
            TableOffset = tableOffset;
            
        }

        public void GetSubTable(BinaryReader br)
        {
            br.BaseStream.Position = TableOffset;
            while (true)
            {
                var offset = br.ReadUInt64();
                if (offset == 0)
                    break;

                Table.Add(offset);

            }

        }

        public void WriteSubTable(BinaryWriter bw)
        {
            foreach (var item in Table)
                bw.Write(item);

            bw.Write((long)0);
        }
    }
}
