namespace Lib999.Font.PC
{
    public class FontTablePC
    {
        public int Count { get; private set; }
        public int Unknown0 { get; private set; }
        public int Unknown1 { get; private set; }
        public ulong CharInfoStartOffset { get; private set; }
        public List<uint> CharInfoOffsetTable { get; private set; } = new();

        public FontTablePC(int count, int unknown0, int unknown1)
        {
            Count = count;
            Unknown0 = unknown0;
            Unknown1 = unknown1;
            CharInfoStartOffset = 0x14;
        }

        public FontTablePC(BinaryReader br)
        {
            Count = br.ReadInt32();
            Unknown0 = br.ReadInt32();
            Unknown1 = br.ReadInt32();
            CharInfoStartOffset = br.ReadUInt64();
            for (int i = 0; i < Count; i++)
                CharInfoOffsetTable.Add(br.ReadUInt32());

        }

        public void AddCharInfoOffset(int offset)
        {
            CharInfoOffsetTable.Add((uint)(offset >> 1));
        }

        public void WriteSubHeader(BinaryWriter bw)
        {
            bw.Write(Count);
            bw.Write(Unknown0);
            bw.Write(Unknown1);
            bw.Write(CharInfoStartOffset);

        }

        public void WriteTable(BinaryWriter bw)
        {
            foreach (var charOffset in CharInfoOffsetTable)
                bw.Write(charOffset);

        }

    }
}
