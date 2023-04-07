using System.Text.RegularExpressions;

namespace Lib999.Text
{
    public class CharaTexts
    {
        public SirHeader Header { get; set; }
        public VLQTable DataReadList { get; set; }
        public SirStrings StringBlock { get; set; }
        public SirSubTableV3 TablesOffsets { get; set; }

        public CharaTexts(string path)
        {


            GetData(path);
            var texts = string.Join("\r\n\r\n", StringBlock.Strings);
            var dest = $"999_exported\\{path.Replace(Path.GetFileName(path), "")}";
            Directory.CreateDirectory(dest);
            File.WriteAllText($"{dest}\\{Path.GetFileName(path)}.txt", texts);


        }

        public CharaTexts(string datPath, string txtPath)
        {
            GetData(datPath);
            DataReadList = new();

            var txtScript = File.ReadAllText(txtPath).Replace("\r", "").Replace("\n", "").Replace("<ID:", "~<ID:")
               .Split(new string[] { "~" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var dlgs = new List<Dialog999>();

            foreach (var dlg in txtScript)
            {
                var id = Regex.Match(dlg, "<ID.*?>").Value;

                dlgs.Add(new Dialog999(0, 0,
                dlg.Replace(id, string.Empty), 0));
            }

            MemoryStream dat = new MemoryStream();


            using (BinaryWriter bw = new(dat))
            {
                // bw.Write(File.ReadAllBytes(fsbPath));

                bw.BaseStream.Position = 0x10;

                StringBlock.ReplaceDialogsWithSubTable(dlgs, null, bw);

                for (int y = 0; y < StringBlock.Dialogs.Count; y++)
                    TablesOffsets.SubTable[y] = StringBlock.Dialogs[y].Offset;

                bw.AlignBy(4);

                var stringBlockSize = (uint)bw.BaseStream.Position - 8;
                var offset0 = (int)bw.BaseStream.Position;
                TablesOffsets.WriteSubTable(bw);
                bw.Write(0);


               
                bw.Write(offset0);
                bw.Write(0);
                bw.AlignBy(16);

                var offset1 = (int)bw.BaseStream.Position;
                Header = new SirHeader(offset0, offset1);
                CreateDataRead(stringBlockSize);
                DataReadList.WriteDataVLQ(bw);
                bw.Write((byte)0);
                bw.AlignBy(16);
                bw.BaseStream.Position = 0;
                Header.Write(bw);
            }
            var dest = $"999_converted\\{datPath.Replace(Path.GetFileName(datPath), "")}";
            Directory.CreateDirectory(dest);
            File.WriteAllBytes($"{dest}\\{Path.GetFileName(datPath)}", dat.ToArray());
        }

        private void CreateDataRead(uint stringBlockSize)
        {

            DataReadList.DecompressedValues = new List<uint>() { 4, 4 };
            DataReadList.DecompressedValues.Add(stringBlockSize);

            foreach (var item in TablesOffsets.SubTable)
            {
                if (item < 0x10)
                    DataReadList.DecompressedValues[DataReadList.DecompressedValues.Count - 1] = 8;
                
                else
                    DataReadList.DecompressedValues.Add(4);
                
                
            }

            DataReadList.DecompressedValues[DataReadList.DecompressedValues.Count - 1] = 8;

            DataReadList.ConvertToDataVLQ();
        }

        public void GetData(string path)
        {
            using var br = new BinaryReader(File.OpenRead(path));

            Header = new SirHeader(br);
            br.BaseStream.Position = Header.Offset0;
            TablesOffsets = new SirSubTableV3(br);
            StringBlock = new SirStrings(br, TablesOffsets);
            StringBlock.CreateAScriptV3();
            br.BaseStream.Position = Header.Offset1;
            DataReadList = new VLQTable(br);
            br.Close();
        }
    }
}
