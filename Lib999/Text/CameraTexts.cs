using System.Text.RegularExpressions;

namespace Lib999.Text
{
    public class CameraTexts
    {
        public SirHeader Header { get; set; }
        public VLQTable DataReadList { get; set; }
        public List<SirStrings> StringBlock { get; set; }
        public List<SirSubTableV6> TablesOffsets { get; set; }

        public CameraTexts(string path)
        {
            GetData(path);
            var texts = string.Join("\r\n\r\n", StringBlock.Select(x => string.Join("\r\n", x.Strings)).ToList());
            var dest = $"999_exported\\{path.Replace(Path.GetFileName(path), "")}";
            Directory.CreateDirectory(dest);
            File.WriteAllText($"{dest}\\{Path.GetFileName(path)}.txt", texts);
        }

        private void GetData(string path)
        {
            StringBlock = new();
            TablesOffsets = new();
            using var br = new BinaryReader(File.OpenRead(path));

            Header = new SirHeader(br);
            br.BaseStream.Position = Header.Offset0;
            while (true)
            {
                var offset = br.ReadUInt32();
                if (offset == 0)
                    break;

                br.BaseStream.Position -= 4;
                TablesOffsets.Add(new SirSubTableV6(br));
            }

            TablesOffsets.ForEach(x => x.GetSubTable(br));
            TablesOffsets.ForEach(x => StringBlock.Add(new SirStrings(br, x)));
            StringBlock.ForEach(x => x.CreateAScriptV5());

            br.BaseStream.Position = Header.Offset1;
            DataReadList = new VLQTable(br);
            br.Close();
        }

        public CameraTexts(string datPath, string txtPath)
        {
            StringBlock = new();
            TablesOffsets = new();
            GetData(datPath);
            DataReadList = new();

            var txtScript = File.ReadAllText(txtPath).Replace("\r", "").Replace("\n", "").Replace("<FILE_SCRIPT>", "~<FILE_SCRIPT>")
                .Split(new string[] { "~" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var titleAreas = new List<List<Dialog999>>();
            var descAreas = new List<List<Dialog999>>();
            const string toReaplce0 = "<FILE_SCRIPT>";

            foreach (var script in txtScript)
            {
                var stgs = script.Replace("<END>", "<END>~").Split(new string[] { "~" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                var dlgs = new List<Dialog999>();
                var commandArea = new List<Dialog999>()
                {
                  new Dialog999(0, 0, stgs[0].Replace(toReaplce0, string.Empty), 0)

                };

                titleAreas.Add(commandArea);

                for (int i = 1; i < stgs.Count; i++)
                {
                    var id = Regex.Match(stgs[i], "<ID.*?>").Value;

                    dlgs.Add(new Dialog999(0, 0,
                       stgs[i].Replace(id, string.Empty), 0));
                }


                descAreas.Add(dlgs);
            }

            MemoryStream dat = new MemoryStream();


            using (BinaryWriter bw = new(dat))
            {
                // bw.Write(File.ReadAllBytes(fsbPath));

                bw.BaseStream.Position = 0x10;

                for (int i = 0; i < StringBlock.Count; i++)
                {
                    StringBlock[i].ReplaceDialogsWithSubTable(descAreas[i], titleAreas[i], bw);
                    TablesOffsets[i].Title1Offset = StringBlock[i].Title1.Offset;

                    for (int y = 0; y < StringBlock[i].Dialogs.Count; y++)
                        TablesOffsets[i].SubTable[y] = StringBlock[i].Dialogs[y].Offset;

                }

                bw.AlignBy(4);

                var stringBlockSize = (uint)bw.BaseStream.Position - 8;

                for (int i = 0; i < TablesOffsets.Count; i++)
                {
                    TablesOffsets[i].SubTableOffset = (uint)bw.BaseStream.Position;
                    TablesOffsets[i].WriteSubTable(bw);
                }

                var offset0 = (int)bw.BaseStream.Position;

                TablesOffsets.ForEach(t => t.Write(bw));
                bw.Write(0);
                bw.Write(offset0);
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

            foreach (var item in TablesOffsets)
            {
                var count = item.SubTable.Count / 3;
                if (count > 1)
                {
                    var list = new List<uint>();
                    Enumerable.Range(0, count - 1).ToList().ForEach(x => list.AddRange(new uint[] { 4, 4, 0x10 }));
                    DataReadList.DecompressedValues.AddRange(list);
                }
               
                DataReadList.DecompressedValues.AddRange(new uint[] { 4, 4, 0x14 });
            }

            var list2 = new List<uint>();
            Enumerable.Range(0, TablesOffsets.Count * 2).ToList().ForEach(x => list2.AddRange(new uint[] { 4 }));
            DataReadList.DecompressedValues.AddRange(Enumerable.Range(0, (TablesOffsets.Count * 2) - 1).ToList().Select(x => 4U).ToList());
            DataReadList.DecompressedValues.Add(8);
            DataReadList.ConvertToDataVLQ();
        }
    }
}
