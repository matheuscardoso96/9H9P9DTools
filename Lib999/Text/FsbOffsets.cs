namespace Lib999.Text
{
    public class FsbSubHeader
    {
        public uint OffsetTrueFileName { get; set; }
        public uint EventScriptTableffset { get; set; }
        public uint MainStringTableOffset { get; set; }
        public uint MainStringBlockCount { get; set; }
        public uint OffsetStartBlockTable { get; set; }
        public uint OffsetFourthTable { get; set; }

        public FsbSubHeader(BinaryReader br)
        {
            OffsetTrueFileName = br.ReadUInt32();
            EventScriptTableffset = br.ReadUInt32();
            MainStringBlockCount = br.ReadUInt32();
            MainStringTableOffset = br.ReadUInt32();
            OffsetStartBlockTable = br.ReadUInt32();
            OffsetFourthTable = br.ReadUInt32();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(OffsetTrueFileName);
            bw.Write(EventScriptTableffset);
            bw.Write(MainStringBlockCount);
            bw.Write(MainStringTableOffset);
            bw.Write(OffsetStartBlockTable);
            bw.Write(OffsetFourthTable);
        }
    }

    public class RoomSubHeader
    {
        public Dictionary<uint,uint> OffsetMainAndChildren { get; set; }

        public RoomSubHeader(BinaryReader br)
        {
            OffsetMainAndChildren = new Dictionary<uint, uint>();
            while (true) 
            {
                var offset = br.ReadUInt32();
                if (offset == 0) break;
                var childrenOffset = br.ReadUInt32();
                OffsetMainAndChildren.Add(offset, childrenOffset);
            }
        }

        public RoomSubHeader()
        {
            OffsetMainAndChildren = new();
        }

        public void OffsetMainAndChildrenAddItem(uint key, uint value) 
        {
            OffsetMainAndChildren.Add(key, value);
        }

        public void Write(BinaryWriter bw)
        {
            foreach (var item in OffsetMainAndChildren)
            {
                bw.Write(item.Key);
                bw.Write(item.Value);
            }
        }
    }
}