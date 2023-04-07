namespace Lib999
{
    public class SirHeaderPC
    {
        public int Magic { get; private set; }
        public long Offset0 { get; private set; }
        public long Offset1 { get; private set; }

        public SirHeaderPC(BinaryReader br)
        {
            Magic = br.ReadInt32();
            Offset0 = br.ReadInt64();
            Offset1 = br.ReadInt64();
        }

        public SirHeaderPC(long offset0, long offset1)
        {
            Magic = 0x31524953;
            Offset0 = offset0;
            Offset1 = offset1;
        }

        public void SetOffset0(long offset0) => Offset0 = offset0;

        public void SetOffset1(long offset1) => Offset1 = offset1;

        public void Write(BinaryWriter bw)
        {
            bw.Write(Magic);
            bw.Write(Offset0);
            bw.Write(Offset1);
        }
    }
}
