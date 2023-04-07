using System.Text.RegularExpressions;

namespace Lib999.Text
{
    public class FsbPc
    {
        public SirHeaderPC Header { get; set; }
        public SirStringsPc StringBlock { get; set; }

        public FsbPc(string path)
        {
            using var br = new BinaryReader(File.OpenRead(path));

            Header = new SirHeaderPC(br);
            br.BaseStream.Position = Header.Offset0 + 16;
            StringBlock = new SirStringsPc(br);

            StringBlock.CreateAScript(br);
            File.WriteAllLines(Path.GetFileName(path) + ".txt", StringBlock.Strings);
            // File.WriteAllText(Path.GetFileName(path) + ".event.txt", StringBlock.EventScript);

        }
 

        public FsbPc(string fsbPath, string txtScriptPath)
        {
            var txtScript = File.ReadAllText(txtScriptPath)
                .Split(new string[] { "<END>" }, StringSplitOptions.RemoveEmptyEntries)
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
                Header = new SirHeaderPC(br);
                br.BaseStream.Position = Header.Offset0 + 8;
                StringBlock = new SirStringsPc(br);
                br.Close();
            }



            MemoryStream memoryStream = new MemoryStream();

            using (BinaryWriter bw = new(memoryStream))
            {
                bw.Write(File.ReadAllBytes(fsbPath));
                StringBlock.ReplaceDialogs(dlgs999, bw);
                File.WriteAllBytes("teste.fsb", memoryStream.ToArray());
                bw.Close();
            }


        }


    }
}
