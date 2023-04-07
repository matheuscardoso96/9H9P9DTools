using System.Text.RegularExpressions;

namespace Lib999.Text
{
    public class FsbTexts
    {
        public SirHeader Header { get; set; }
        public SirStrings StringBlock { get; set; }
        public VLQTable DataReadList { get; set; }

        public FsbTexts(string path, bool exportEvents)
        {
           
            using var br = new BinaryReader(File.OpenRead(path));
            var destPath = "999_exported\\" + path.Replace(Path.GetFileName(path),"");
            Header = new SirHeader(br);
            br.BaseStream.Position = Header.Offset0 + 8;
            StringBlock = new SirStrings(br);
            Directory.CreateDirectory(destPath);
            StringBlock.CreateAScript(br);
            File.WriteAllLines($"{destPath}\\{Path.GetFileName(path)}.txt", StringBlock.Strings);
            br.BaseStream.Position = Header.Offset1;
            DataReadList = new VLQTable(br);
            if (exportEvents)
                File.WriteAllText($"{destPath}\\{Path.GetFileName(path)}.event.txt", StringBlock.EventScriptFinal.ToString());
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


            var dlgs999 = new List<Dialog999>();

            foreach (var item in txtScript)
            {
                var id = Regex.Match(item, "<ID.*?>").Value;
                var idValue = Convert.ToInt32(Regex.Match(id, "(\\d+)").Value);

                dlgs999.Add(new Dialog999(idValue, 0, item.Replace(id, ""), 0));
            }

            dlgs999 = dlgs999.OrderBy(d => d.Id).GroupBy(x => x.Id).Select(x => x.First()).ToList();
            //var ordered = 

            using (var br = new BinaryReader(File.OpenRead(fsbPath)))
            {
                Header = new SirHeader(br);
                br.BaseStream.Position = Header.Offset0 + 8;
                StringBlock = new SirStrings(br);
                br.Close();
            }



            MemoryStream memoryStream = new MemoryStream();
            Directory.CreateDirectory("Convertd_fsb");

            using (BinaryWriter bw = new(memoryStream))
            {
                bw.Write(File.ReadAllBytes(fsbPath));
                StringBlock.ReplaceDialogsWithIds(dlgs999, bw);
                var dest = $"999_converted\\{fsbPath.Replace(Path.GetFileName(fsbPath), "")}";
                Directory.CreateDirectory(dest);
                File.WriteAllBytes($"{dest}\\{Path.GetFileName(fsbPath)}", memoryStream.ToArray());
                bw.Close();
            }
             
            
        }


    }
}
