namespace Lib999.Text
{
    public class SirTextsV2
    {
        public SirHeader Header { get; set; }
        public VLQTable DataReadList { get; set; }
        public SirStrings StringBlock { get; set; }
        public SirSubTableV2 TablesOffsets { get; set; }

        public SirTextsV2(string path)
        {
           
            using var br = new BinaryReader(File.OpenRead(path));

            Header = new SirHeader(br);
            br.BaseStream.Position = Header.Offset0;
            TablesOffsets = new SirSubTableV2(br);
            TablesOffsets.GetSubTable(br);
            StringBlock = new SirStrings(br, TablesOffsets);
            StringBlock.CreateAScriptV2();
            var texts = string.Join("\r\n\r\n", StringBlock.Strings);
            br.BaseStream.Position = Header.Offset1;
            DataReadList = new VLQTable(br);
            br.Close();
            File.WriteAllText(Path.GetFileName(path) + ".txt", texts);


        }
    }
}
