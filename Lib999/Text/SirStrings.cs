using System.Text;

namespace Lib999.Text
{
    public class SirStrings
    {
        public uint StgsCount { get; set; }
        public uint StringTablePosition { get; set; }
        public Dialog999 Title1 { get; set; } = null;
        public Dialog999 Title2 { get; set; } = null;
        public Dialog999 Title3 { get; set; } = null;
        public List<Dialog999> Dialogs { get; set; } = new();
        public List<string> Strings { get; set; } = new List<string>();
        public List<CommandEvent> EventDialogs { get; set; } = new();
        //public string EventScript { get; set; } = "";
        public StringBuilder EventScriptFinal { get; set; } = new();
        public Encoding JapaneseEncoding { get; private set; }

        public SirStrings(BinaryReader br)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            JapaneseEncoding = Encoding.GetEncoding(932);
            
            InitSjisTables();
            StgsCount = br.ReadUInt32();
            StringTablePosition = br.ReadUInt32();
            br.BaseStream.Position = StringTablePosition;
            for (int i = 0; i < StgsCount; i++)
            {
                br.BaseStream.Position = StringTablePosition + i * 4;
                var offset = br.ReadUInt32();
                var text = GetString(offset, br);
                Dialogs.Add(new Dialog999(i, offset, text, (int)(br.BaseStream.Position - offset)));
            }


        }

        public SirStrings(BinaryReader br, SirSubTableV1 table)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            JapaneseEncoding = Encoding.GetEncoding(932);
            InitSjisTables();
            StgsCount = (uint)table.SubTable.Count;
            Title1 = new Dialog999((int)table.Title1Offset, table.Title1Offset, GetString(table.Title1Offset, br), 0);
            Title2 = new Dialog999((int)table.Title2Offset, table.Title2Offset, GetString(table.Title2Offset, br), 0);
            Title3 = new Dialog999((int)table.Title3Offset, table.Title3Offset, GetString(table.Title3Offset, br), 0);
            for (int i = 0; i < table.SubTable.Count; i++)
            {
                var text = GetString(table.SubTable[i], br);
                Dialogs.Add(new Dialog999(i, table.SubTable[i], text, (int)(br.BaseStream.Position - table.SubTable[i])));
            }

        }

        public SirStrings(BinaryReader br, SirSubTableV2 table)
        {
            InitSjisTables();
            Title1 = new Dialog999(0, table.Title1Offset, GetString(table.Title1Offset, br), 0);
            StgsCount = (uint)table.SubTable.Count;

            for (int i = 0; i < table.SubTable.Count; i++)
            {
                var text = GetString(table.SubTable[i], br);
                Dialogs.Add(new Dialog999(i, table.SubTable[i], text, (int)(br.BaseStream.Position - table.SubTable[i])));
            }

        }

        public SirStrings(BinaryReader br, SirSubTableV3 table)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            JapaneseEncoding = Encoding.GetEncoding(932);
            InitSjisTables();
            StgsCount = (uint)table.SubTable.Count;

            for (int i = 0; i < table.SubTable.Count; i++)
            {
                if (table.SubTable[i] >= 0x10)
                {
                    var text = GetString(table.SubTable[i], br);
                    Dialogs.Add(new Dialog999(i, table.SubTable[i], text, (int)(br.BaseStream.Position - table.SubTable[i])));
                }
                else
                {
                    Dialogs.Add(new Dialog999(0, 0,$"<CODE: {table.SubTable[i]}>" , 0));
                }
                
            }

        }

        public SirStrings(BinaryReader br, SirSubTablePCV1 subTable)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            JapaneseEncoding = Encoding.GetEncoding(932);
            InitSjisTables();
            StgsCount = (uint)subTable.Table.Count;

            for (int i = 0; i < subTable.Table.Count; i++)
            {
                var text = GetString((uint)subTable.Table[i], br);
                Dialogs.Add(new Dialog999(i, subTable.Table[i], text, (int)(br.BaseStream.Position - (long)subTable.Table[i])));
            }

        }

        public SirStrings(BinaryReader br, SirSubTableV4 table)
        {
            InitSjisTables();
            StgsCount = (uint)table.SubTable.Count;
            Title1 = new Dialog999(0, table.Title1Offset, GetString(table.Title1Offset, br), 0);

            for (int i = 0; i < table.SubTable.Count; i++)
            {
                var text = GetString(table.SubTable[i], br);
                Dialogs.Add(new Dialog999(i, table.SubTable[i], text, (int)(br.BaseStream.Position - table.SubTable[i])));
            }

        }

        public SirStrings(BinaryReader br, SirSubTableV5 table)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            JapaneseEncoding = Encoding.GetEncoding(932);
            InitSjisTables();
            StgsCount = (uint)table.SubTable.Count - 5;
            Title1 = new Dialog999(0, table.Title1Offset, GetString(table.Title1Offset, br), 0);

            for (int i = 0; i < table.SubTable.Count; i++)
            {
                var text = GetString(table.SubTable[i], br);
                Dialogs.Add(new Dialog999(i, table.SubTable[i], text, (int)(br.BaseStream.Position - table.SubTable[i])));
            }

        }

        public SirStrings(BinaryReader br, SirSubTableV6 table)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            JapaneseEncoding = Encoding.GetEncoding(932);
            InitSjisTables();
            StgsCount = (uint)table.SubTable.Count - 5;
            Title1 = new Dialog999(0, table.Title1Offset, GetString(table.Title1Offset, br), 0);

            for (int i = 0; i < table.SubTable.Count; i++)
            {
                var text = GetString(table.SubTable[i], br);
                Dialogs.Add(new Dialog999(i, table.SubTable[i], text, (int)(br.BaseStream.Position - table.SubTable[i])));
            }

        }

        public void CreateAScript(BinaryReader br)
        {
            _ = ScanEventArea(br);
            

            foreach (var ev in EventDialogs)
            {
                if (ev.Description.Contains("comand0x28"))
                {
                    var command = Dialogs[(ev.Args[0] << 2) / 4].Text;

                    Strings.Add("\r\n");

                    NameTags.TryGetValue(command.Replace("<END>", ""), out var nameTag);
                    if (nameTag != null)
                        Strings.Add($"{nameTag}<END>");
                    else
                        Strings.Add(command);


                    EventScriptFinal.Append(ev?.Description);
                    EventScriptFinal.Append(ev?.FinalDesc.Replace($"{ev.Args[0]}>",$"{Dialogs[(ev.Args[0] << 2) / 4].Text.Replace("<END>", "")}>"));
                }
                else if (ev.Description.Contains("print_msg"))
                {
                    Strings.Add($"<ID: {ev.Args[0]}>\r\n{Dialogs[(ev.Args[0] << 2) / 4].Text}");
                    EventScriptFinal.Append(ev?.Description);
                    EventScriptFinal.Append(ev?.FinalDesc);
                }
                else if (ev.Description.Contains("comand0x0D") && ev.Args[0] == 0xF4)
                {
                    var codes = ev.FinalDesc.Split(" ");
                    var codeInt = Convert.ToInt32(codes[2]);
                    var comandName = Dialogs[(codeInt << 2) / 4].Text.Replace("<END>", "");


                    if (comandName.Contains("?System") || comandName.Contains("?Sound") 
                        || comandName.Contains("?BG") || comandName.Contains("?View") 
                        || comandName.Contains("?Call") 
                        || comandName.Contains("?Item")) 
                    {
                        var subName = "";
                        var codeInt2 = 0;
                        codeInt2 = Convert.ToInt32(codes[3].Replace(">", ""));
                        if (codeInt2 == 0x9B)
                        {

                        }
                        subName = Dialogs[(codeInt2 << 2) / 4].Text.Replace("<END>", "");
                        EventScriptFinal.Append(ev?.Description);
                        EventScriptFinal.Append($": 244 {comandName} {subName}>");
                    }
                    else
                    {
                        EventScriptFinal.Append(ev?.Description);
                        EventScriptFinal.Append(ev?.FinalDesc.Replace($": 244 {codeInt}", $": 244 {comandName}"));
                    }



                }
                else
                {
                    EventScriptFinal.Append(ev?.Description);
                    EventScriptFinal.Append(ev?.FinalDesc);
                }


            }
        }

        public void CreateAScriptV1()
        {

            Strings.Add($"<FILE_TITLE_CMD>\r\n{Title1.Text}");
            Strings.Add($"<FILE_TITLE>\r\n{Title2.Text}");
            Strings.Add($"<FILE_DESC_CMD>\r\n{Title3.Text}");

            foreach (var dlg in Dialogs)
            {
                Strings.Add($"<ID: {dlg.Id}>\r\n{dlg.Text}");

            }
        }

        public void CreateAScriptV2()
        {

            Strings.Add($"<ID: -1>\r\n{Title1.Text}");


            foreach (var dlg in Dialogs)
            {
                Strings.Add($"<ID: {dlg.Id}>\r\n{dlg.Text}");

            }
        }

        public void CreateAScriptV3() => Dialogs.ForEach(dlg => Strings.Add($"<ID: {dlg.Id}>\r\n{dlg.Text}"));

        public void CreateAScriptV4(List<uint> unknowCodes)
        {

            //Strings.Add($"<ID_Offset: {Title1.Offset}>\r\n{Title1.Text}");
            Strings.Add($"<UNKNOWN_CODES>\r\n{string.Join(",",unknowCodes)}<END>");
            Strings.Add($"<FILE_TITLE_CMD>\r\n{Title1.Text}");

            foreach (var dlg in Dialogs)
            {
                Strings.Add($"<ID: {dlg.Id}>\r\n{dlg.Text}");

            }
        }

        public void CreateAScriptV5()
        {

            //Strings.Add($"<ID_Offset: {Title1.Offset}>\r\n{Title1.Text}");
           // Strings.Add($"<UNKNOWN_CODES>\r\n{string.Join(",", unknowCodes)}<END>");
            Strings.Add($"<FILE_SCRIPT>\r\n{Title1.Text}");

            foreach (var dlg in Dialogs)
            {
                Strings.Add($"<ID: {dlg.Id}>\r\n{dlg.Text}");

            }
        }

        public void CreateScriptPCV1() 
        {
            bool theNextIsTag = false;

            foreach (var item in Dialogs)
            {

                

                if (theNextIsTag)
                {
                    theNextIsTag = false;

                    NameTags.TryGetValue($"&{item.Text.Replace("<END>", "")}", out var nameTag);
                    if (nameTag != null)
                        Strings.Add($"[{item.Text.Replace("<END>", "")}]|{nameTag}<END>");
                    else
                        Strings.Add(item.Text);
                }
                else
                {
                    Strings.Add(item.Text);
                }

                if (item.Text.Equals("Talk<END>"))
                    theNextIsTag = true;
            }

            
        }

        private string GetString(uint offset, BinaryReader br)
        {
            
            br.BaseStream.Position = offset;
            StringBuilder text = new();
            
            int code;
            do
            {
                code = br.ReadByte();
                if (code == 0xE1)
                {

                }

                if (code >= 0x20 && code < 0x7F) //|| code >= 0xE0 && code < 0xFD)
                    text.Append(Convert.ToChar(code));
                else if (code >= 0xA1 && code <= 0xDD)
                {
                    var index = Array.IndexOf(SjisCompTbl, code);
                    var sjisCode = SjisDecompTbl[index];
                    var bytes = BitConverter.GetBytes(sjisCode).Take(2).ToArray();
                    var tex = JapaneseEncoding.GetString(bytes);
                    text.Append(tex);
                }
                else if (code == 0x00) text.Append(SpecialChars.First().Value.Description);
                else if (code >= 0x80)
                {

                    var specialCharCode = (code << 8) + br.ReadByte();
                    SpecialChars.TryGetValue(specialCharCode, out var special);
                    if (special != null)
                    {
                        text.Append(special.Description);
                        text.Append(GetTextArgs(special, br));

                    }

                    else
                    {
                        if (specialCharCode > 0x81)
                        {
                            var bytes = Array.Empty<byte>();
                            bytes = BitConverter.GetBytes(specialCharCode).Take(2).Reverse().ToArray();


                            var tex = JapaneseEncoding.GetString(bytes);
                            text.Append(tex);
                        }
                        else
                        {
                            text.Append($"<0x{specialCharCode.ToString("X2")}>");
                        }

                    }

                    // text.Append($"<0x{specialCharCode.ToString("X2")}>");


                }

                else
                {
                    text.Append($"<0x{code.ToString("X2")}>");
                }





            } while (code != 0x00);



            return text.ToString();
        }

        public string GetTextArgs(CommandEvent commandEvent, BinaryReader br)
        {
            StringBuilder sb = new();
            var arg = 0;
            switch (commandEvent.ArgType)
            {
                case "short":

                    for (int i = 0; i < commandEvent.ArgCount; i++)
                    {
                        arg = br.ReadUInt16();
                        sb.Append(arg);

                        if (i < commandEvent.ArgCount - 2)
                            sb.Append(" ");
                    }

                    return $": {sb}>";

                case "byte":

                    for (int i = 0; i < commandEvent.ArgCount; i++)
                    {
                        arg = br.ReadByte();
                        sb.Append($" {arg}");
                    }

                    if (commandEvent.Description == "<Cmd")
                    {
                        if (arg != 0x4E && arg != 0x6E && arg != 0x77 && arg != 0x43 && arg != 0x42)
                        {
                            sb.Append($" {br.ReadByte()}");
                            sb.Append($" {br.ReadByte()}");
                        }
                        else if (arg == 0x43 || arg == 0x42)
                        {
                            sb.Append($" {br.ReadByte()}");
                        }
                        else if (arg == 0x77)
                        {
                            sb.Append($" {br.ReadByte()}");
                            sb.Append($" {br.ReadByte()}");
                            sb.Append($" {br.ReadByte()}");
                            sb.Append($" {br.ReadByte()}");
                        }

                    }

                    return $":{sb}>";

                default:
                    return "";
            }
        }

        private string ScanEventArea(BinaryReader br)
        {
            var eventPart = new StringBuilder();
            br.BaseStream.Position = 0x10;
            var offesetLimit = Dialogs.First().Offset;

            InitEventCommands();

            while (br.BaseStream.Position < offesetLimit)
            {
                var code = br.ReadByte();
          

                EventCommands.TryGetValue(code, out var command);
                if (command != null)
                {
                    var clone = (CommandEvent)command.Clone();
                    eventPart.Append(clone?.Description);
                    clone?.GetArgs(br);
                    eventPart.Append(clone?.FinalDesc);

                    EventDialogs.Add(clone);

                }
                else
                {
                    EventDialogs.Add(new CommandEvent() { FinalDesc = "<0x" + code.ToString("X2") + ">" });
                   
                }
            }

            return eventPart.ToString();

        }

        private void InitEventCommands()
        {
            var commandTbl = File.ReadLines("commands.tbl");

            foreach (var item in commandTbl)
            {
                var entry = item.Split('º');
                var code = Convert.ToInt32(entry[0], 16);
                var description = entry[1];
                var argcount = Convert.ToInt32(entry[2]);

                if (argcount > 0)
                {
                    var argType = entry[3];
                    EventCommands.Add(code, new CommandEvent { Description = description, ArgCount = argcount, ArgType = argType });
                }
                else
                {
                    EventCommands.Add(code, new CommandEvent { Description = description });
                }
            }
        }

        private void InitSjisTables()
        {
            SjisCompTbl = File.ReadAllBytes("Sjis_Comp_Tbl.bin").Select(x => (int)x).ToArray();

            using (BinaryReader br = new BinaryReader(File.OpenRead("Sjis_Decomp_Tbl.bin")))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                    SjisDecompTbl.Add(br.ReadInt16());

            }

        }

        private void InitSpecialCharsCode()
        {

            foreach (var item in SpecialChars)
                SpecialCharsCode.Add(item.Value.Description.Replace("\r", "").Replace("\n", ""), item.Key);
        }

        public void ReplaceDialogsWithIds(List<Dialog999> dialogsToReplace, BinaryWriter bw)
        {
            InitSpecialCharsCode();
            var firstOriginalDialogFromFsbOffset = Dialogs.First().Offset;
            int stringBlockTotalSize = (int)(Dialogs.Last().Offset + Dialogs.Last().Lenght - firstOriginalDialogFromFsbOffset);

            foreach (var item in Dialogs)
            {
                var toReplace = dialogsToReplace.FirstOrDefault(x => x.Id == item.Id);

                if (toReplace != null)
                    item.Text = toReplace.Text;

                SetDliag999TextInBytes(item);
            }

            int writeOffset = (int)firstOriginalDialogFromFsbOffset;

            for (int i = 0; i < Dialogs.Count; i++)
            {
                bw.BaseStream.Position = writeOffset;
                Dialogs[i].Offset = (uint)writeOffset;

                if (bw.BaseStream.Position + Dialogs[i].TextInBytes.Length > firstOriginalDialogFromFsbOffset + stringBlockTotalSize)
                {
                    writeOffset = (int)bw.BaseStream.Length;
                    bw.BaseStream.Position = writeOffset;
                    Dialogs[i].Offset = (uint)writeOffset;
                }

                bw.Write(Dialogs[i].TextInBytes);
                writeOffset = (int)bw.BaseStream.Position;
                bw.BaseStream.Position = StringTablePosition + i * 4;
                bw.Write(Dialogs[i].Offset);

            }

        }

        public void ReplaceDialogsWithSubTable(List<Dialog999> descArea, List<Dialog999> titleArea,  BinaryWriter bw)
        {
            InitSpecialCharsCode();

            

            if (titleArea != null && titleArea.Count >= 1)
            {
                Title1.Text = titleArea[0].Text;
                SetDliag999TextInBytes(Title1);
                Title1.Offset = (uint)bw.BaseStream.Position;
                bw.Write(Title1.TextInBytes);
            }

            if (titleArea != null && titleArea.Count >= 2)
            {
                Title2.Text = titleArea[1].Text;
                SetDliag999TextInBytes(Title2);
                Title2.Offset = (uint)bw.BaseStream.Position;
                bw.Write(Title2.TextInBytes);
            }


            if (titleArea != null && titleArea.Count >= 3)
            {
                Title3.Text = titleArea[2].Text;
                SetDliag999TextInBytes(Title3);
                Title3.Offset = (uint)bw.BaseStream.Position;
                bw.Write(Title3.TextInBytes);
            }

            for (int i = 0; i < descArea.Count; i++)
                Dialogs[i].Text = descArea[i].Text;

            foreach (var dialog in Dialogs)
            {
                if (dialog.Text.Contains("<CODE:")) 
                    dialog.Offset = Convert.ToUInt32(dialog.Text.Split(':')[1].Replace(">",""));             
                else
                    SetDliag999TextInBytes(dialog);
                
            }


            

            for (int i = 0; i < Dialogs.Count; i++)
            {
                if (Dialogs[i].TextInBytes != null && Dialogs[i].TextInBytes.Length > 0) 
                {
                    Dialogs[i].Offset = (uint)bw.BaseStream.Position;
                    bw.Write(Dialogs[i].TextInBytes);
                } 

            }

        }

        public void ReplaceDialogsWithSubTablePC(List<Dialog999> descArea, List<Dialog999> titleArea, BinaryWriter bw)
        {
            InitSpecialCharsCode();



            if (titleArea != null && titleArea.Count >= 1)
            {
                Title1.Text = titleArea[0].Text;
                SetDliag999TextInBytes(Title1);
                Title1.Offset = (uint)bw.BaseStream.Position;
                bw.Write(Title1.TextInBytes);
            }

            if (titleArea != null && titleArea.Count >= 2)
            {
                Title2.Text = titleArea[1].Text;
                SetDliag999TextInBytes(Title2);
                Title2.Offset = (uint)bw.BaseStream.Position;
                bw.Write(Title2.TextInBytes);
            }


            if (titleArea != null && titleArea.Count >= 3)
            {
                Title3.Text = titleArea[2].Text;
                SetDliag999TextInBytes(Title3);
                Title3.Offset = (uint)bw.BaseStream.Position;
                bw.Write(Title3.TextInBytes);
            }

            for (int i = 0; i < descArea.Count; i++)
                Dialogs[i].Text = descArea[i].Text;

            foreach (var dialog in Dialogs)
            {
                if (dialog.Text.Contains("<CODE:"))
                    dialog.Offset = Convert.ToUInt32(dialog.Text.Split(':')[1].Replace(">", ""));
                else
                    SetDliag999TextInBytesPC(dialog);

            }




            for (int i = 0; i < Dialogs.Count; i++)
            {
                if (Dialogs[i].TextInBytes != null && Dialogs[i].TextInBytes.Length > 0)
                {
                    Dialogs[i].Offset = (uint)bw.BaseStream.Position;
                    bw.Write(Dialogs[i].TextInBytes);
                }

            }

        }

        private void SetDliag999TextInBytes(Dialog999 dialog)
        {
            Console.WriteLine($"Convertendo texto para bytes: {dialog.Text}\r\n----------------------------");
            List<byte> bytes = new List<byte>();

            for (int i = 0; i < dialog.Text.Length; i++)
            {
                if (dialog.Text[i] == '<' || dialog.Text[i] == '[')
                {
                    var finalizer = dialog.Text[i] == '<' ? '>' : ']';
                    StringBuilder tag = new();
                    tag.Append(dialog.Text[i]);
                    i++;
                    while (dialog.Text[i] != finalizer)
                    {
                        tag.Append(dialog.Text[i]);
                        i++;
                    }

                    tag.Append(dialog.Text[i]);

                    bytes.AddRange(TagAnalyseToBytes(tag.ToString()));
                }

                else
                {
                    var chara = dialog.Text[i];
                    SpecialCharsCode.TryGetValue($"{chara}", out var code);
                    if (code != 0)
                        bytes.AddRange(BitConverter.GetBytes(code).Take(2).Reverse().ToArray());
                    else
                    {
                        var charValue = Convert.ToChar($"{dialog.Text[i]}");
                        if (charValue > 0x1000)
                        {
                            var japanseChar = JapaneseEncoding.GetBytes($"{dialog.Text[i]}");
                            bytes.AddRange(japanseChar);
                        }
                        else
                        {
                            bytes.Add((byte)charValue);
                        }

                    }





                }
            }

            dialog.TextInBytes = bytes.ToArray();
        }

        private void SetDliag999TextInBytesPC(Dialog999 dialog)
        {
            if (dialog.Text == "Ow!")
            {

            }
            List<byte> bytes = new List<byte>();

         

            for (int i = 0; i < dialog.Text.Length; i++)
            {
                if (dialog.Text[i] == '<' || dialog.Text[i] == '[')
                {
                    var finalizer = dialog.Text[i] == '<' ? '>' : ']';
                    StringBuilder tag = new();
                    tag.Append(dialog.Text[i]);
                    i++;
                    while (dialog.Text[i] != finalizer)
                    {
                        tag.Append(dialog.Text[i]);
                        i++;
                    }

                    tag.Append(dialog.Text[i]);

                    bytes.AddRange(TagAnalyseToBytes(tag.ToString()));
                }

                else
                {
                    var chara = dialog.Text[i];
                    SpecialCharsCode.TryGetValue($"{chara}", out var code);
                    if (code != 0)
                        bytes.AddRange(BitConverter.GetBytes(code).Take(2).Reverse().ToArray());
                    else
                    {
                        var charValue = Convert.ToChar($"{dialog.Text[i]}");
                        if (charValue > 0x1000)
                        {
                            var japanseChar = JapaneseEncoding.GetBytes($"{dialog.Text[i]}");
                            bytes.AddRange(japanseChar);
                        }
                        //else if (charValue >= 0xC1 && charValue <= 0xFA)
                        //{
                        //    PcACents.TryGetValue($"{chara}", out var codeAcent);
                        //    var japanseChar = BitConverter.GetBytes(codeAcent).Take(2).Reverse().ToList();
                        //    bytes.AddRange(japanseChar);

                        //}
                        else
                        {
                            bytes.Add((byte)charValue);
                        }

                    }





                }
            }

            dialog.TextInBytes = bytes.ToArray();
        }

        private byte[] TagAnalyseToBytes(string tag)
        {
            if (tag.Contains(":"))
            {
                var tagPart = tag.Split(':');
                SpecialCharsCode.TryGetValue(tagPart[0], out var code);
                if (code == 0)
                    return new byte[] { 0x00 };

                var intBytes = BitConverter.GetBytes(code).Take(2).Reverse().ToList();

                var args = tagPart[1].Replace(">", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var item in args)
                    intBytes.Add(Convert.ToByte(item));

                return intBytes.ToArray();
            }
            else
            {

                if (tag.Contains("<END>"))
                    return new byte[] { 0x00 };

                if (tag.ToLower().Contains("0x"))
                    return BitConverter.GetBytes(Convert.ToInt32(tag.Replace("<", "").Replace(">", ""), 16)).Take(2).Reverse().ToArray();

                SpecialCharsCode.TryGetValue(tag, out var code);
                if (code == 0)
                    return new byte[] { 0x00 };

                byte[] intBytes = BitConverter.GetBytes(code).Take(2).Reverse().ToArray();


                return intBytes;
            }
        }


        private void Save()
        {

        }

        private Dictionary<string, string> NameTags = new()
        {

            ["&NOVEL"] = "&NOVEL",
            ["&HERO"] = "&HERO",
            ["&Q"] = "&Q",
            ["&茜２"] = "[Akane]",
            ["&茜"] = "[Akane]",
            ["&ゼロ"] = "[Zero]",
            ["&踊り子２"] = "[Dancer2]",
            ["&踊り子"] = "[Dancer]",
            ["&銀髪"] = "[Silver]",
            ["&獅子翁"] = "[Lion]",
            ["&桃色髪"] = "[Pink hair]",
            ["&王子"] = "[Prince]",
            ["&岩男"] = "[Mountain]",
            ["&鳥の巣"] = "[Bird's Nest]",
            ["&淳平"] = "[Junpei]",
            ["&四葉"] = "[Yotsuba/Clover]",
            ["&サンタ"] = "[Santa]",
            ["&ニルス"] = "[Nils/Snake]",
            ["&セブン"] = "[Seven]",
            ["&一宮"] = "[Ichinomiya/Ace]",
            ["&八代"] = "[Yashiro/Lotus]",
            ["&紫"] = "[Murasaki/June]",
            ["&？？？１"] = "[???]",
            ["&？？？"] = "[???]",



        };

        private Dictionary<string, int> PcACents = new()
        {

             ["á"] = 0x81E1,
             ["Á"] = 0x81C1


        };

        private Dictionary<int, CommandEvent> EventCommands = new();

        private int[] SjisCompTbl = Array.Empty<int>();
        private List<short> SjisDecompTbl = new();
        private Dictionary<int, CommandEvent> SpecialChars = new()
        {
            [0x00] = new CommandEvent { Description = "<END>", ArgCount = 0 },
            [0x8140] = new CommandEvent { Description = "<Tab>" },
            [0x8145] = new CommandEvent { Description = "[.]", ArgCount = 0 },
            [0x8148] = new CommandEvent { Description = "？", ArgCount = 0 },
            [0x815B] = new CommandEvent { Description = "ー", ArgCount = 0 },
            [0x815C] = new CommandEvent { Description = "―", ArgCount = 0 },
            [0x8163] = new CommandEvent { Description = "…", ArgCount = 0 },
            [0x8169] = new CommandEvent { Description = "（", ArgCount = 0 },
            [0x816A] = new CommandEvent { Description = "）", ArgCount = 0 },
            [0x8173] = new CommandEvent { Description = "《", ArgCount = 0 },
            [0x8174] = new CommandEvent { Description = "》", ArgCount = 0 },
            [0x8175] = new CommandEvent { Description = "「", ArgCount = 0 },
            [0x8176] = new CommandEvent { Description = "」", ArgCount = 0 },
            [0x8177] = new CommandEvent { Description = "『", ArgCount = 0 },
            [0x8178] = new CommandEvent { Description = "』", ArgCount = 0 },
            [0x8179] = new CommandEvent { Description = "【", ArgCount = 0 },
            [0x817A] = new CommandEvent { Description = "】", ArgCount = 0 },
            [0x81A0] = new CommandEvent { Description = "□", ArgCount = 0 },
            [0x81A2] = new CommandEvent { Description = "△", ArgCount = 0 },
            [0x81A3] = new CommandEvent { Description = "▲", ArgCount = 0 },
            [0x81A4] = new CommandEvent { Description = "▽", ArgCount = 0 },
            [0x81A5] = new CommandEvent { Description = "<FINAL>", ArgCount = 0 }, //▼
            [0x81A8] = new CommandEvent { Description = "→", ArgCount = 0 },
            [0x81F4] = new CommandEvent { Description = "♪", ArgCount = 0 },
            [0x8263] = new CommandEvent { Description = "\"", ArgCount = 0 },
            [0x8272] = new CommandEvent { Description = "'", ArgCount = 0 },
            [0x84A0] = new CommandEvent { Description = "<kanji0>", ArgCount = 0 },
            [0x84A1] = new CommandEvent { Description = "<kanji1>", ArgCount = 0 },
            [0x84A2] = new CommandEvent { Description = "<kanji2>", ArgCount = 0 },
            [0x84A3] = new CommandEvent { Description = "<pá>", ArgCount = 0 },
            [0x84A4] = new CommandEvent { Description = "<bumerangue>", ArgCount = 0 },
            [0x84A5] = new CommandEvent { Description = "<maçã>", ArgCount = 0 },
            [0x84A6] = new CommandEvent { Description = "<arco>", ArgCount = 0 },
            [0x84A7] = new CommandEvent { Description = "<martelo>", ArgCount = 0 },
            [0x84A8] = new CommandEvent { Description = "<catavento>", ArgCount = 0 },
            [0x84A9] = new CommandEvent { Description = "<vazio>", ArgCount = 0 },
            [0x84AA] = new CommandEvent { Description = "<pião>", ArgCount = 0 },
            [0x84AB] = new CommandEvent { Description = "<borboleta>", ArgCount = 0 },
            [0x84AC] = new CommandEvent { Description = "<comida0>", ArgCount = 0 },
            [0x84AD] = new CommandEvent { Description = "<comida1>", ArgCount = 0 },
            [0x84AE] = new CommandEvent { Description = "<comida2>", ArgCount = 0 },
            [0x84AF] = new CommandEvent { Description = "<losango>", ArgCount = 0 },
            [0x84B0] = new CommandEvent { Description = "<rubi>", ArgCount = 0 },
            [0x84B1] = new CommandEvent { Description = "<diamente>", ArgCount = 0 },
            [0x84B2] = new CommandEvent { Description = "<pedra>", ArgCount = 0 },
            [0x84B3] = new CommandEvent { Description = "<NADA>", ArgCount = 0 },
            [0x84B4] = new CommandEvent { Description = "<lapis>", ArgCount = 0 },
            [0x84B5] = new CommandEvent { Description = "<monitor>", ArgCount = 0 },
            [0x84B6] = new CommandEvent { Description = "<escudo>", ArgCount = 0 },
            [0x84B7] = new CommandEvent { Description = "<exclamação>", ArgCount = 0 },
            [0x84B8] = new CommandEvent { Description = "<cruz>", ArgCount = 0 },
            [0x84B9] = new CommandEvent { Description = "<seilá>", ArgCount = 0 },
            [0x84BA] = new CommandEvent { Description = "<sapo>", ArgCount = 0 },
            [0x84BB] = new CommandEvent { Description = "<pc>", ArgCount = 0 },
            [0x84BC] = new CommandEvent { Description = "<quadrado>", ArgCount = 0 },
            [0x84BD] = new CommandEvent { Description = "<coracao>", ArgCount = 0 },
            [0x84BE] = new CommandEvent { Description = "<café>", ArgCount = 0 },
            [0x8752] = new CommandEvent { Description = "<P>", ArgCount = 0 },
            [0x8753] = new CommandEvent { Description = "<Cmd", ArgCount = 1, ArgType = "byte" }


        };

        private Dictionary<string, int> SpecialCharsCode = new();

    }
}
