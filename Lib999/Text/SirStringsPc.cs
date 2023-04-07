using System.Text;

namespace Lib999.Text
{
    public class SirStringsPc
    {
        public uint StgsCount { get; set; }
        public long StringTablePosition { get; set; }
        public List<Dialog999PC> Dialogs { get; set; } = new();
        public List<string> Strings { get; set; } = new List<string>();
        public List<CommandEvent> EventDialogs { get; set; } = new();
        public string EventScript { get; set; } = "";
        public SirStringsPc(BinaryReader br)
        {
            InitSjisTables();
            StgsCount = br.ReadUInt32();
            StringTablePosition = br.ReadInt64();
            br.BaseStream.Position = StringTablePosition;
            for (int i = 0; i < StgsCount; i++)
            {
                br.BaseStream.Position = StringTablePosition + i * 8;
                var offset = br.ReadInt64();
                var text = GetString(offset, br);
                Dialogs.Add(new Dialog999PC(i, offset, text, (int)(br.BaseStream.Position - offset)));
            }


        }

        public void CreateAScript(BinaryReader br)
        {
            EventScript = ScanEventArea(br);


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



                }
                else if (ev.Description.Contains("print_msg"))
                {
                    Strings.Add($"<ID: {ev.Args[0]}>\r\n{Dialogs[(ev.Args[0] << 2) / 4].Text}");
                }


            }
        }

        private string GetString(long offset, BinaryReader br)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            br.BaseStream.Position = offset;
            StringBuilder text = new();
            var japaneseEncoding = Encoding.GetEncoding(932);
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
                    var tex = japaneseEncoding.GetString(bytes);
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


                            var tex = japaneseEncoding.GetString(bytes);
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
                /*if (code == 0x28)
                {
                    var secondCode = br.ReadByte();
                    if (secondCode >= 0x01 && secondCode < 0xFF)
                    {
                        br.BaseStream.Position += 2;
                        EventDialogsIds.Add((br.ReadUInt16() << 2) / 4);
                    }
                }*/

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
                    eventPart.Append("<" + code.ToString("X2") + ">");
                }
            }

            return eventPart.ToString();

        }

        private void InitEventCommands()
        {
            var commandTbl = File.ReadLines("commandsPC.tbl");

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

        public void ReplaceDialogs(List<Dialog999> dialogsToReplace, BinaryWriter bw)
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

        private void SetDliag999TextInBytes(Dialog999PC dialog)
        {

            List<byte> bytes = new List<byte>();

            for (int i = 0; i < dialog.Text.Length; i++)
            {
                if (dialog.Text[i] == '<')
                {
                    StringBuilder tag = new();
                    tag.Append(dialog.Text[i]);
                    i++;
                    while (dialog.Text[i] != '>')
                    {
                        tag.Append(dialog.Text[i]);
                        i++;
                    }

                    tag.Append(dialog.Text[i]);

                    bytes.AddRange(TagAnalyseToBytes(tag.ToString()));
                }
                else
                {
                    SpecialCharsCode.TryGetValue($"{dialog.Text[i]}", out var code);
                    if (code != 0)
                        bytes.AddRange(BitConverter.GetBytes(code).Take(2).Reverse().ToArray());
                    else
                        bytes.Add((byte)Convert.ToChar($"{dialog.Text[i]}"));




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

        private Dictionary<int, CommandEvent> EventCommands = new();

        private int[] SjisCompTbl = Array.Empty<int>();
        private List<short> SjisDecompTbl = new();
        private Dictionary<int, CommandEvent> SpecialChars = new()
        {
            [0x00] = new CommandEvent { Description = "<END>", ArgCount = 0 },
            [0x8140] = new CommandEvent { Description = "<Tab>" },
            [0x8145] = new CommandEvent { Description = "[.]", ArgCount = 0 },
            [0x8148] = new CommandEvent { Description = "？", ArgCount = 0 },
            [0x815C] = new CommandEvent { Description = "ー", ArgCount = 0 },
            [0x8163] = new CommandEvent { Description = "…", ArgCount = 0 },
            [0x816B] = new CommandEvent { Description = "（", ArgCount = 0 },
            [0x816C] = new CommandEvent { Description = "）", ArgCount = 0 },
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
