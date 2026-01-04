namespace Lib999
{
    public class SirHeader 
    {
        public int Magic { get; set; }
        public int Offset0 { get; set; }
        public int Offset1 { get; set; }
        public int Unknown { get; set; }

        public SirHeader(BinaryReader br)
        {
            Magic = br.ReadInt32();
            Offset0 = br.ReadInt32();
            Offset1 = br.ReadInt32();
            Unknown = br.ReadInt32();
        }

        public SirHeader(int offset0, int offset1)
        {
            Magic = 0x30524953;
            Offset0 = offset0;
            Offset1 = offset1;
            Unknown = 0;
        }

        public void SetOffset0(int offset0) => Offset0 = offset0;
       
        public void SetOffset1(int offset1)  => Offset1 = offset1;

        public void Write(BinaryWriter bw) 
        {
            bw.Write(Magic);
            bw.Write(Offset0);
            bw.Write(Offset1);
            bw.Write(Unknown);
        }
    }
}
