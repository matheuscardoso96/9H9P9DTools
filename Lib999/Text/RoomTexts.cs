using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lib999.Text
{
    public class RoomTexts
    {
        public SirHeader Header { get; set; }
        public VLQTable DataReadList { get; set; }
        public RoomSubHeader RoomSubHeader { get; set; }
        public List<string> MainStrings { get; set; } = new();
        public List<SirStrings> Blocks { get; set; } = new();

        public RoomTexts(string path)
        {
            using var br = new BinaryReader(File.OpenRead(path));
            GetData(br);

           
        }

        public void RomTextstoTxt (string path)
        {
            var finalText = new StringBuilder();

            var counter = 0;

            foreach (var b in Blocks)
            {

                finalText.Append($"----Start of area-----:{MainStrings[counter]}\r\n");

                foreach (var dlg in b.Dialogs)
                {

                    finalText.Append($"{dlg.Text}\r\n\r\n");
                }


                finalText.Append($"----End of area-----:{MainStrings[counter]}\r\n");

                counter++;
            }


            path = path.Substring(2);
            var dest = $"999_exported\\{path.Replace(Path.GetFileName(path), "")}";
            Directory.CreateDirectory(dest);
            File.WriteAllText($"{dest}\\{Path.GetFileName(path)}.txt", finalText.ToString());
        }

        private void GetData(BinaryReader br)
        {
            Header = new SirHeader(br);
            br.BaseStream.Position = Header.Offset0;
            RoomSubHeader = new RoomSubHeader(br);


            foreach (var item in RoomSubHeader.OffsetMainAndChildren) 
            {
                br.BaseStream.Seek(item.Key, SeekOrigin.Begin);
                MainStrings.Add(SirUtils.ReadNullTerminatedString(br));
                br.BaseStream.Seek(item.Value, SeekOrigin.Begin);

                var sirStg = new SirStrings(br, item.Value);
                
                Blocks.Add(sirStg);
            }

            br.BaseStream.Position = Header.Offset1;
            DataReadList = new VLQTable(br);
         
           
        }

        

        public void RoomTextsToDat(string datPath, string txtPath)
        {

            Header = new SirHeader(0,0);
            RoomSubHeader = new ();
            var sirStrings = new SirStrings();

            var dlgs = new List<Dialog999>();

            foreach (string linha in File.ReadLines(txtPath))
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                if (linha.Contains("-Start of area-"))
                {
                    var mainText = linha.Split(':')[1];
                    var dlgMain = new Dialog999(0, 0, $"{mainText}<END>", 0)
                    {
                        IsMainString = true
                    };

                    sirStrings.SetDliag999TextInBytes(dlgMain);

                    dlgs.Add(dlgMain);
                    continue;
                } else if (linha.Contains("-End of area-")) { continue; }

                var dlg = new Dialog999(0, 0, linha, 0);
                sirStrings.SetDliag999TextInBytes(dlg, txtPath.Contains("staff.dat.txt"));

                dlgs.Add(dlg);

            }

            MemoryStream dat = new MemoryStream();


            using (BinaryWriter bw = new(dat))
            {
                Header.Write(bw);

                foreach (var item in dlgs)
                {
                    item.Offset = (uint)bw.BaseStream.Position;
                    bw.Write(item.TextInBytes);
                }

                bw.AlignBy(4);

                var stringBlockSize = (uint)bw.BaseStream.Position - 8;

                var lastMainOffset = 0u;
                bool writeEnd = false;

                foreach (var item in dlgs)
                {
                    if (item.IsMainString) 
                    {
                        lastMainOffset = item.Offset;
                        if (writeEnd) 
                        {
                          bw.Write(0u);
                        }
                        
                        writeEnd = true;

                    }

                    if (!item.IsMainString)
                    {
                        if (lastMainOffset > 0) 
                        {
                            RoomSubHeader.OffsetMainAndChildrenAddItem(lastMainOffset, (uint)bw.BaseStream.Position);
                            lastMainOffset = 0;
                        }

                        bw.Write(item.Offset);
                    }
                    
                }

                bw.Write(0u);

                Header.Offset0 = (int)bw.BaseStream.Position;

                RoomSubHeader.Write(bw);

                bw.Write(0u);

                bw.Write(Header.Offset0);

                bw.AlignBy(16);

                DataReadList.DecompressedValues[2] = stringBlockSize;
                DataReadList.DataVLQ = new();
                DataReadList.ConvertToDataVLQ();
                Header.Offset1 = (int)bw.BaseStream.Position;
                DataReadList.WriteDataVLQ(bw);
                bw.Write((byte)0x0);
                bw.AlignBy(16);
                bw.BaseStream.Position = 0;
                Header.Write(bw);
            }
            var dest = $"999_converted\\{datPath.Replace(Path.GetFileName(datPath), "")}";
            Directory.CreateDirectory(dest);
            File.WriteAllBytes($"{dest}\\{Path.GetFileName(datPath)}", dat.ToArray());
        }
    }
}
