using System.Text;
using System.Text.RegularExpressions;
using Lib999.Text;

namespace Lib999.Text
{
    public class FsbTexts
    {
        public SirHeader Header { get; set; }
        public SirStrings MainStringBlock { get; set; }
        public SirStrings EventStringsBlock { get; set; }
        public SirStrings StartStringBlock { get; set; }
        public SirStrings StringBlock4 { get; set; }
        public VLQTable DataReadList { get; set; }
        public FsbSubHeader FsbSubHeader { get; set; }
        public string TrueFileName { get; set; }

        private void ExportAllStringBlocks(string destPath, string path)
        {
            var allBlocks = new StringBuilder();

           

            AppendBlock(MainStringBlock, FsbSubHeader.MainStringTableOffset, "StringBlock", allBlocks);
            AppendBlock(EventStringsBlock, FsbSubHeader.EventScriptTableffset + 4, "StringBlock2", allBlocks);
            AppendBlock(StartStringBlock, FsbSubHeader.OffsetStartBlockTable, "StringBlock3", allBlocks);
            AppendBlock(StringBlock4, FsbSubHeader.OffsetFourthTable, "StringBlock4", allBlocks);

            File.WriteAllText(Path.Combine(destPath, $"AllStringBlocks_{Path.GetFileNameWithoutExtension(path)}.txt"), allBlocks.ToString(), Encoding.UTF8);

            var allCommandStrings = MainStringBlock;

            var commandChars = new char[] { '$', '^', '~', '&', '?', '@', ':' };

            var commandStrings = MainStringBlock.Dialogs
                //.Where(dlg => dlg.TextInBytes.Length > 1 && commandChars.Contains(dlg.Text[0]))
                .Select(d => $"{d.Text} | <ID: Int {d.Id} HEX {d.Id.ToString("X")}> <OFFSET: {d.Offset.ToString("X")}>").ToList();

            File.WriteAllLines(Path.Combine(destPath, $"{Path.GetFileNameWithoutExtension(path)}_CommandStrings.txt"), commandStrings, Encoding.UTF8);


        }

        void AppendBlock(SirStrings block, uint offset, string blockName, StringBuilder allBlocks)
        {
            if (block != null)
            {
                // Calculate the total size of the block in bytes, including null terminators (\0)
                uint totalSize = (uint)block.Dialogs.Sum(d => d.Lenght);

                // Round up to the next multiple of 4
                totalSize = (totalSize + 3) & ~3U;

                allBlocks.AppendLine($"--- {blockName} (Offset: 0x{offset:X}, Size: 0x{totalSize:X} bytes) ---");
                foreach (var str in block.Dialogs)
                {
                    if (str.Id == 709)
                    {

                    }
                    allBlocks.AppendLine(str.Text);
                }
                allBlocks.AppendLine();
            }
        }

        public FsbTexts(string path)
        {
            using var br = new BinaryReader(File.OpenRead(path));
            GetFsbData(br);
        }

        public void FsbToTxt(string path, bool exportEvents)
        {
           
            var destPath = "999_exported\\" + path.Replace(Path.GetFileName(path), "");
            Directory.CreateDirectory(destPath);

            File.WriteAllLines($"{destPath}\\{Path.GetFileName(path)}.txt", MainStringBlock.Strings);
            
            if (exportEvents)
                File.WriteAllText($"{destPath}\\{Path.GetFileName(path)}.event.txt", MainStringBlock.EventScriptFinal.ToString());


            using (var bw = new BinaryWriter(File.Create($"{destPath}\\{Path.GetFileName(path)}_datareadlist.bin")))
            {
                foreach (var item in DataReadList.DecompressedValues)
                {
                    if (item > 4)
                        bw.Write((short)item);
                    else
                    {
                        bw.Write((byte)item);
                    }
                }
            }

            if (exportEvents)
            {
                ExportAllStringBlocks(destPath, path);
            }
            
        }

        private void GetFsbData(BinaryReader br)
        {
            Header = new SirHeader(br);
            br.BaseStream.Position = Header.Offset0;
            FsbSubHeader = new FsbSubHeader(br);

            br.BaseStream.Position = FsbSubHeader.OffsetTrueFileName;
            TrueFileName = SirUtils.ReadNullTerminatedString(br);

            br.BaseStream.Position = FsbSubHeader.EventScriptTableffset;

            EventStringsBlock = new SirStrings(br, FsbSubHeader.EventScriptTableffset, true);

            MainStringBlock = new SirStrings(br, FsbSubHeader.MainStringBlockCount, FsbSubHeader.MainStringTableOffset);

            if (EventStringsBlock.Dialogs.Count > 4)
            {
                Console.WriteLine($"Vários blocos -> {TrueFileName} -> {EventStringsBlock.Dialogs.Count}");
            }


            StartStringBlock = new SirStrings(br, FsbSubHeader.OffsetStartBlockTable);

            StringBlock4 = new SirStrings(br, FsbSubHeader.OffsetFourthTable);
            br.BaseStream.Position = Header.Offset1;
            DataReadList = new VLQTable(br);
            MainStringBlock.CreateAScriptV2(br, EventStringsBlock);

        }

        public FsbTexts(string pathJpnScripts, string pathEngScripts, string dualPath)
        {
            var jpnScripts = Directory.GetFiles(pathJpnScripts, "*.fsb");
            var engScripts = Directory.GetFiles(pathEngScripts, "*.fsb");

            Directory.CreateDirectory("dual");

            foreach (var item in jpnScripts)
            {
                using var br1 = new BinaryReader(File.OpenRead(item));             
                var header = new SirHeader(br1);
                br1.BaseStream.Position = header.Offset0 + 8;
                var stringBlock = new SirStrings(br1);
                stringBlock.CreateAScript(br1);

                var engScriptPath = engScripts.FirstOrDefault(x => Path.GetFileName(x) == Path.GetFileName(item));

                using var br2 = new BinaryReader(File.OpenRead(engScriptPath));
                var header2 = new SirHeader(br2);
                
                br2.BaseStream.Position = header2.Offset0 + 8;
                var stringBlock2 = new SirStrings(br2);
                stringBlock2.CreateAScript(br2);

                var justJpnWithIds = stringBlock.Strings.Where(x => x.Contains("<ID:")).ToList();
                var justEngWithIds = stringBlock2.Strings.Where(x => x.Contains("<ID:")).ToList();

                if (justEngWithIds.Count > justJpnWithIds.Count)
                {
                    int counter = 0;
                    for (int i = 0; i < stringBlock2.Strings.Count ; i++)
                    {
                       
                        if (stringBlock2.Strings[i].Contains("<ID:") && counter < justJpnWithIds.Count)
                        {
                            stringBlock2.Strings[i] += $"\r\n{justJpnWithIds[counter]}";
                            counter++;
                        }
                    }
                }
                else
                {
                    int counter = 0;
                    for (int i = 0; i < stringBlock2.Strings.Count; i++)
                    {
                        if (stringBlock2.Strings[i].Contains("<ID:"))
                        {
                            stringBlock2.Strings[i] += $"\r\n{justJpnWithIds[counter]}";
                            counter++;
                        }
                    }

                    var remaing = justJpnWithIds.Count - (counter - 1);

                    stringBlock2.Strings[stringBlock2.Strings.Count - 1] += $"\r\n{string.Join("\r\n",justJpnWithIds.Skip(counter).Take(remaing))}" ;
                }

                File.WriteAllLines($"dual\\{Path.GetFileName(item)}.txt", stringBlock2.Strings);
                
            }
        }

        public FsbTexts(string fsbPath, string txtScriptPath)
        {
            var txtScript = File.ReadAllText(txtScriptPath)
                .Split(new string[] { "<END>" } ,StringSplitOptions.RemoveEmptyEntries)
                .Select(dlg => $"{dlg.Replace("\r", "").Replace("\n", "")}<END>")
                .Where(dlg => dlg.Contains("ID")).ToList();

            if (fsbPath.Contains("a32.fsb"))
            {

            }
            var dlgs999 = new List<Dialog999>();

            var commandChars = new char[] { '$', '^', '~', '&', '?', '@', ':' };

            foreach (var item in txtScript)
            {
                var id = Regex.Match(item, "<ID.*?>").Value;
                var idValue = Convert.ToInt32(Regex.Match(id, "(\\d+)").Value);

                var dlg999 = new Dialog999(idValue, 0, item.Replace(id, ""), 0);


                if (dlg999.Text.Length > 0 && dlg999.Text[0] == ' ')
                {
                    //remove first char space
                    dlg999.Text = dlg999.Text.Substring(1);
                }
                string output = Regex.Replace(dlg999.Text, "<.*?>", "");

               


                if (string.IsNullOrEmpty(output))
                {
                    continue;
                }

                if (commandChars.Contains(output[0]))
                {
                    dlg999.IsCommand = true;
                }

                dlgs999.Add(dlg999);
            }

            dlgs999 = dlgs999.OrderBy(d => d.Id).GroupBy(x => x.Id).Select(x => x.First()).ToList();
            //var ordered = 
            var eventAreaBackup = new List<byte>();

            using (var br = new BinaryReader(File.OpenRead(fsbPath)))
            {
                GetFsbData(br);
                var eventAreaSize = MainStringBlock?.Dialogs.First().Offset - 0x10;
                br.BaseStream.Position = 0x10;
                eventAreaBackup.AddRange(br.ReadBytes((int)eventAreaSize));

                MainStringBlock?.ReplaceDialogsWithIdsSimple(dlgs999);

               

            }

            MemoryStream memoryStream = new MemoryStream();

            using (BinaryWriter bw = new(memoryStream))
            {

                for (int i = 0; i < 4; i++)
                {
                    bw.Write(0);
                }
                
                bw.Write(eventAreaBackup.ToArray());

                //MainStringBlock.Dialogs = MainStringBlock.Dialogs.OrderBy(d => d.TextInBytes[0]).ToList();

                //sort MainStringBlock by bytes
                MainStringBlock.Dialogs = MainStringBlock.Dialogs
                    .OrderBy(d => d.TextInBytes, Comparer<byte[]>.Create((x, y) =>
                    {
                        for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
                        {
                            int comparison = x[i].CompareTo(y[i]);
                            if (comparison != 0)
                                return comparison;
                        }
                        return x.Length.CompareTo(y.Length);
                    }))
                    .ToList();


                for (int i = 0; i < MainStringBlock.Dialogs.Count; i++)
                {
                    MainStringBlock.Dialogs[i].NewId = i;

                }


                foreach (var st in MainStringBlock?.Dialogs)
                {
                    st.Offset = (uint)bw.BaseStream.Position;
                    bw.Write(st.TextInBytes);
                }

                while (bw.BaseStream.Position % 4 != 0)
                    bw.Write((byte)0xAA);

                FsbSubHeader.MainStringTableOffset = (uint)bw.BaseStream.Position;

                DataReadList.DecompressedValues[2] = (uint)bw.BaseStream.Position - 8;


                FsbSubHeader.MainStringBlockCount = (uint)MainStringBlock?.Dialogs.Count;

                foreach (var st in MainStringBlock?.Dialogs)
                {
                    bw.Write(st.Offset);

                }

                bw.Write((uint)0);

                foreach (var st in StartStringBlock?.Dialogs)
                {
                    st.Offset = (uint)bw.BaseStream.Position;
                    bw.Write(st.TextInBytes);
                }
                
                while (bw.BaseStream.Position % 4 != 0)
                    bw.Write((byte)0xAA);

                FsbSubHeader.OffsetStartBlockTable = (uint)bw.BaseStream.Position;

                foreach (var st in StartStringBlock?.Dialogs)
                {
                    bw.Write(st.Offset);

                }

                bw.Write((uint)0);


                foreach (var st in StringBlock4?.Dialogs)
                {
                    st.Offset = (uint)bw.BaseStream.Position;
                    bw.Write(st.TextInBytes);

                }

                while (bw.BaseStream.Position % 4 != 0)
                    bw.Write((byte)0xAA);

                FsbSubHeader.OffsetFourthTable = (uint)bw.BaseStream.Position;

                foreach (var st in StringBlock4?.Dialogs)
                {
                    bw.Write(st.Offset);

                }

                bw.Write((uint)0);

                foreach (var st in EventStringsBlock?.Dialogs)
                {

                    if (!st.IsEventData)
                    {
                        st.Offset = (uint)bw.BaseStream.Position;
                        bw.Write(st.TextInBytes);
                    }

                }

                while (bw.BaseStream.Position % 4 != 0)
                    bw.Write((byte)0xAA);


                FsbSubHeader.EventScriptTableffset = (uint)bw.BaseStream.Position;


                foreach (var st in EventStringsBlock?.Dialogs)
                {
                    bw.Write(st.Offset);

                }

                bw.Write((uint)0);
                bw.Write((uint)0);

                var trueFileNameOffset = (uint)bw.BaseStream.Position;
                bw.Write(Encoding.ASCII.GetBytes($"{TrueFileName}\0"));

                while (bw.BaseStream.Position % 4 != 0)
                    bw.Write((byte)0xAA);

                Header.Offset0 = (int)bw.BaseStream.Position;

                FsbSubHeader.OffsetTrueFileName = trueFileNameOffset;

                FsbSubHeader.Write(bw);

                while (bw.BaseStream.Position % 16 != 0)
                    bw.Write((byte)0xAA);

                DataReadList.DataVLQ = new();

                DataReadList.ConvertToDataVLQ();

                Header.Offset1 = (int)bw.BaseStream.Position;

                DataReadList.WriteDataVLQ(bw);
                bw.Write((byte)0x0);

                while (bw.BaseStream.Position % 16 != 0)
                    bw.Write((byte)0xAA);

                bw.BaseStream.Position = 0;
                Header.Write(bw);

                foreach (var st in MainStringBlock?.EventDialogs)
                {
                    //if (!st.Description.Contains("print_msg"))
                    //{
                    //    continue;
                    //}

                    if (st.OffsetsWithStrings.Count > 0)
                    {
                        foreach (var off in st.OffsetsWithStrings)
                        {
                            if (off.HasString)
                            {
                                var dlg = MainStringBlock.Dialogs.First(d => d.Id == off.Code);

                                //if (dlg.NewId != off.Code) 
                                //{
                                    bw.BaseStream.Position = off.Offset;

                                    switch (off.CodeType)
                                    {
                                        case "ushort":
                                            bw.Write((ushort)dlg.NewId);
                                            break;
                                        case "byte":
                                            bw.Write((byte)dlg.NewId);
                                            break;
                                        default:
                                            break;
                                    }
                               // }

                            }
                        }
                    }
                }

                var dest = $"999_converted\\{fsbPath.Replace(Path.GetFileName(fsbPath), "")}";
                Directory.CreateDirectory(dest);
                File.WriteAllBytes($"{dest}\\{Path.GetFileName(fsbPath)}", memoryStream.ToArray());
            }


        }

        


    }
}
