using System.Text.RegularExpressions;

namespace Lib999.Text
{
    public class FileTexts
    {
        public SirHeader? Header { get; set; }
        public VLQTable DataReadList { get; set; }
        public List<SirStrings> StringBlocks { get; set; }
        public List<SirSubTableV1> TablesOffsets { get; set; }

        public FileTexts(string path)
        {
            StringBlocks = new();
            TablesOffsets = new();
            GetData(path);
            var texts = string.Join("\r\n\r\n", StringBlocks.Select(x => string.Join("\r\n", x.Strings)).ToList());
            var dest = $"999_exported\\{path.Replace(Path.GetFileName(path),"")}";
            Directory.CreateDirectory(dest);
            File.WriteAllText($"{dest}\\{Path.GetFileName(path)}.txt", texts);


        }

        private void GetData(string path) 
        {
            
            using var br = new BinaryReader(File.OpenRead(path));

            Header = new SirHeader(br);
            br.BaseStream.Position = Header.Offset0;
            while (true)
            {
                var offset = br.ReadUInt32();
                if (offset == 0)
                    break;

                br.BaseStream.Position -= 4;
                TablesOffsets.Add(new SirSubTableV1(br));
            }

            TablesOffsets.ForEach(x => x.GetSubTable(br));
            TablesOffsets.ForEach(x => StringBlocks.Add(new SirStrings(br, x)));
            StringBlocks.ForEach(x => x.CreateAScriptV1());
            br.BaseStream.Position = Header.Offset1;
            DataReadList = new VLQTable(br);
            br.Close();
        }

        public FileTexts(string datPath, string txtPath)
        {
            StringBlocks = new();
            TablesOffsets = new();
            GetData(datPath);
            DataReadList = new();

            var txtScript = File.ReadAllText(txtPath).Replace("\r","").Replace("\n", "").Replace("<FILE_TITLE_CMD>", "~<FILE_TITLE_CMD>")
                .Split(new string[] { "~" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var titleAreas = new List<List<Dialog999>>();
            var descAreas = new List<List<Dialog999>>();
            const string toReaplce0 = "<FILE_TITLE_CMD>";
            const string toReaplce1 = "<FILE_TITLE>";
            const string toReaplce2 = "<FILE_DESC_CMD>";

            foreach (var script in txtScript)
            {
                var stgs = script.Replace("<END>","<END>~").Split(new string[] { "~" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                var dlgs = new List<Dialog999>();
                var commandArea = new List<Dialog999>() 
                {
                  new Dialog999(0, 0, stgs[0].Replace(toReaplce0, string.Empty), 0),
                  new Dialog999(0, 0, stgs[1].Replace(toReaplce1, string.Empty), 0),
                  new Dialog999(0, 0, stgs[2].Replace(toReaplce2, string.Empty), 0)
                };

                titleAreas.Add(commandArea);

                for (int i = 3; i < stgs.Count; i++)
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

                for (int i = 0; i < StringBlocks.Count; i++) 
                {
                    StringBlocks[i].ReplaceDialogsWithSubTable(descAreas[i], titleAreas[i], bw);

                    TablesOffsets[i].Title1Offset = StringBlocks[i].Title1.Offset;
                    TablesOffsets[i].Title2Offset = StringBlocks[i].Title2.Offset;
                    TablesOffsets[i].Title3Offset = StringBlocks[i].Title3.Offset;

                    for (int y = 0; y < StringBlocks[i].Dialogs.Count; y++)
                        TablesOffsets[i].SubTable[y] = StringBlocks[i].Dialogs[y].Offset;
                    
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
                Header = new SirHeader(offset0,offset1);
                CreateDataRead(stringBlockSize);
                /*if (bw.BaseStream.Position % 16 != 0)
                    DataReadList.DataVLQArea[DataReadList.DataVLQArea.Count - 1] = 8;*/
                
                DataReadList.WriteDataVLQ(bw);
                bw.Write((byte)0);
                bw.AlignBy(16);
                bw.BaseStream.Position = 0;
                Header.Write(bw);
                /*StringBlocks.ReplaceDialogsWithIds(dlgs999, bw);
               
                bw.Close(); */
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
                var count = item.SubTable.Count - 1;
                var list = Enumerable.Range(0, count).Select(x => 4U).ToList();
                DataReadList.DecompressedValues.AddRange(list);
                DataReadList.DecompressedValues.Add(8);
            }

            
             var list2 = Enumerable.Range(0, TablesOffsets.Count * 4).Select(x => 4U).ToList();
             DataReadList.DecompressedValues.AddRange(list2.Take(list2.Count - 1));
             DataReadList.DecompressedValues.Add(8);
             DataReadList.ConvertToDataVLQ();
        }


    }
}
