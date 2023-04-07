namespace Lib999.Text
{
    public class SirSubTableV3
    {
        public List<uint> SubTable { get; set; } = new();
        public List<uint> Code { get; set; } = new();

        public SirSubTableV3(BinaryReader br)
        {
            while (true)
            {
                var offset = (uint)br.ReadUInt32();
                if (offset == 0)
                    break;

                
                    SubTable.Add(offset);
                

                

            }
        }

        public void WriteSubTable(BinaryWriter bw)  => SubTable.ForEach(bw.Write);

        
        
    }
}
