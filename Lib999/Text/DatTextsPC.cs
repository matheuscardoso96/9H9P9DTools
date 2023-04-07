using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lib999.Text
{
    public class DatTextsPC
    {
        public SirHeaderPC Header { get; set; }
        public SirSubTablePCV1 SirSubTablePC { get; set; }
        public VLQTable DataReadList { get; set; }
        public SirStrings StringBlock { get; set; }

        public DatTextsPC(string path)
        {

            GetData(path);
            var texts = string.Join("\r\n\r\n", StringBlock.Strings);
            var dest = $"999_pc_exported\\{path.Replace(Path.GetFileName(path), "")}";
            Directory.CreateDirectory(dest);
            File.WriteAllText($"{dest}\\{Path.GetFileName(path)}.txt", texts);


        }

        public DatTextsPC(string datPath, string txtPath)
        {
            GetData(datPath);
            DataReadList = new();

            var txtScript = File.ReadAllText(txtPath).Replace("\r", "").Replace("\n", "").Replace("<END>", "<END>§")
               .Split(new string[] { "§" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var dlgs = new List<Dialog999>();

            bool theNextIsTag = false;

            foreach (var dlg in txtScript)
            {

                if (theNextIsTag)
                {
                    theNextIsTag = false;
                    var finalTag = dlg.Split('|')[0].Replace("[","").Replace("]", "");
                    dlgs.Add(new Dialog999(0, 0, $"{finalTag}<END>", 0));
                }
                else
                {
                    dlgs.Add(new Dialog999(0, 0, dlg, 0));
                }

                if (dlg.Contains("Talk<END>"))
                    theNextIsTag = true;

            }

            MemoryStream dat = new MemoryStream();


            using (BinaryWriter bw = new(dat))
            {
                // bw.Write(File.ReadAllBytes(fsbPath));

                bw.BaseStream.Position = 0x14;

                StringBlock.ReplaceDialogsWithSubTablePC(dlgs, null, bw);

                for (int y = 0; y < StringBlock.Dialogs.Count; y++)
                    SirSubTablePC.Table[y] = StringBlock.Dialogs[y].Offset;

                bw.AlignBy(4);

                var stringBlockSize = (uint)bw.BaseStream.Position - 12;
                var offset0 = bw.BaseStream.Position;
                SirSubTablePC.WriteSubTable(bw);
                bw.Write((long)0);



                bw.Write(offset0);
                bw.Write((long)0);
                bw.Write((long)0);
                bw.Write((long)0);
                bw.AlignBy(16);

                var offset1 = bw.BaseStream.Position;
                Header = new SirHeaderPC(offset0, offset1);
                CreateDataRead(stringBlockSize);
                DataReadList.WriteDataVLQ(bw);
                bw.Write((byte)0);
                bw.AlignBy(16);
                bw.BaseStream.Position = 0;
                Header.Write(bw);
            }
            var dest = $"999_pc_converted\\{datPath.Replace(Path.GetFileName(datPath), "")}";
            Directory.CreateDirectory(dest);
            File.WriteAllBytes($"{dest}\\{Path.GetFileName(datPath)}", dat.ToArray());
        }

        public void GetData(string path)
        {
            using var br = new BinaryReader(File.OpenRead(path));

            Header = new SirHeaderPC(br);
            br.BaseStream.Position = Header.Offset0;
            SirSubTablePC = new SirSubTablePCV1(Header.Offset0);
            SirSubTablePC.GetSubTable(br);
            StringBlock = new SirStrings(br, SirSubTablePC);
            StringBlock.CreateScriptPCV1();
            br.BaseStream.Position = Header.Offset1;
            DataReadList = new VLQTable(br);
            br.Close();
        }

      

        private void CreateDataRead(uint stringBlockSize)
        {

            DataReadList.DecompressedValues = new List<uint>() { 4, 8 };
            DataReadList.DecompressedValues.Add(stringBlockSize);

            foreach (var item in SirSubTablePC.Table)
            {
                if (item < 0x10)
                    DataReadList.DecompressedValues[DataReadList.DecompressedValues.Count - 1] = 8;

                else
                    DataReadList.DecompressedValues.Add(8);


            }

            DataReadList.DecompressedValues[DataReadList.DecompressedValues.Count - 1] = 0x18;

            DataReadList.ConvertToDataVLQ();
        }
    }
}
