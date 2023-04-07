namespace Lib999.Text
{
    public class Dialog999
    {
        public int Id { get; set; }
        public uint Offset { get; set; }
        public ulong LOffset { get; set; }
        public string Text { get; set; }
        public int Lenght { get; set; }
        public byte[] TextInBytes { get; set; }

        public Dialog999(int id, uint offset, string text, int length)
        {
            Id = id;
            Offset = offset;
            Text = text;
            Lenght = length;
        }

        public Dialog999(int id, ulong lOffset, string text, int length)
        {
            Id = id;
            LOffset = lOffset;
            Text = text;
            Lenght = length;
        }


    }

    public class Dialog999PC
    {
        public int Id { get; set; }
        public long Offset { get; set; }
        public string Text { get; set; }
        public int Lenght { get; set; }
        public byte[] TextInBytes { get; set; }

        public Dialog999PC(int id, long offset, string text, int length)
        {
            Id = id;
            Offset = offset;
            Text = text;
            Lenght = length;
        }


    }
}
