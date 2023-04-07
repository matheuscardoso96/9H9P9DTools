namespace Lib999.Font
{
    public class FontTable 
    {
        public int Count { get; private set; }
        public int Unknown0 { get; private set; }
        public int Unknown1 { get; private set; }
        public int CharInfoStartOffset { get; private set; }
        public List<ushort> CharInfoOffsetTable { get; private set; } = new();

        public FontTable(int count, int unknown0, int unknown1)
        {
            Count = count;
            Unknown0 = unknown0;
            Unknown1 = unknown1;
            CharInfoStartOffset = 0x10;
        }

        public FontTable(BinaryReader br)
        {
            Count = br.ReadInt32();
            Unknown0 = br.ReadInt32();
            Unknown1 = br.ReadInt32();
            CharInfoStartOffset = br.ReadInt32();
            for (int i = 0; i < Count; i++)
                CharInfoOffsetTable.Add(br.ReadUInt16());

        }

        public void AddCharInfoOffset(int offset) 
        {
            CharInfoOffsetTable.Add((ushort)(offset >> 1));
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
