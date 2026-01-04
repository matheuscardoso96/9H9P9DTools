namespace Lib999.Text
{
    public class OffsetWithString
    {
        public uint Offset { get; set; }
        public uint Code { get; set; }
        public bool HasString { get; set; }
        public string CodeType { get; set; }

        public OffsetWithString(uint offset, uint code, bool hasString, string codeType)
        {
            Offset = offset;
            Code = code;
            HasString = hasString;
            CodeType = codeType;
        }
    }
}