using System.Text;

namespace Lib999.Text
{
    public class CommandEvent : ICloneable
    {
        public byte Code { get; set; }
        public long Offset { get; set; }
        public string Description { get; set; } = "";
        public int ArgCount { get; set; }
        public string ArgType { get; set; } = "";
        public string FinalDesc { get; set; } = "";
        public List<int> Args { get; set; } = new();
        public List<OffsetWithString> OffsetsWithStrings { get; set; } = new();

        public CommandEvent()
        {

        }

        public CommandEvent(string description, int argCount, string argType)
        {
            Description = description;
            ArgCount = argCount;
            ArgType = argType;
        }

        public object Clone()
        {
            return new CommandEvent(Description, ArgCount, ArgType);
        }

        public void GetArgs(BinaryReader br)
        {
         
            StringBuilder sb = new();

            switch (ArgType)
            {
                case "short":
                    
                    for (int i = 0; i < ArgCount; i++)
                    {
                        var arg = br.ReadUInt16();
                        sb.Append(arg);
                        Args[i] = arg;

                        if (i < ArgCount - 2)
                            sb.Append(",");
                    }
                    if (Description.Contains("print") || Description.Contains("0x28"))
                        FinalDesc = $": {sb}>\r\n";              
                    else
                        FinalDesc = $": {sb}>";
                    break;
                case "byte":
                    for (int i = 0; i < ArgCount; i++)
                    {
                        var arg = br.ReadByte();
                        sb.Append($" {arg}");
                        Args[i] = arg;

                        if (i < ArgCount - 2)
                            sb.Append(" ");
                    }


                    if (Description.Equals("<comand0x0D"))
                    {
                        

                        if (Args[0] == 0xF4)
                        {
                            sb.Append($" {br.ReadUInt16()} ");
                            sb.Append($"{br.ReadUInt16()}");
                        }
                        else if (Args[0] == 0xF0)
                        {
                            
                            var secondCode = br.ReadByte();

                            if (secondCode == 00)
                            {
                                sb.Append(" 0");
                            }
                            else
                            {
                                sb.Append($" {br.ReadByte()} ");
                                sb.Append($"{br.ReadByte()}");
                            }
                        }
                    }

                    
                     FinalDesc = $":{sb}>";


                    break;

                default:
                        FinalDesc = ">";
                    break;

            }

            
        }

        public void GetArgsV2(BinaryReader br)
        {
           
            StringBuilder sb = new();

            switch (ArgType)
            {
                case "short":

                    for (int i = 0; i < ArgCount; i++)
                    {
                        var arg = br.ReadUInt16();
                        sb.Append(arg);
                        Args.Add(arg);

                        if (i < ArgCount - 2)
                            sb.Append(",");
                    }

                    if (Description.Contains("print"))
                    {
                        FinalDesc = $": {sb}>\r\n";
                        OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 2, Convert.ToUInt32(sb.ToString()), false, "ushort"));
                    }

                    if (Description.Contains("0x28"))
                    {

                        FinalDesc = $": {sb}>\r\n";
                        OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 2, Convert.ToUInt32(sb.ToString()), false, "ushort"));
                    }

                    if (Description.Contains("0x34"))
                    {

                        FinalDesc = $": {sb}>\r\n";
                        OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 2, Convert.ToUInt32(sb.ToString()), false, "ushort"));
                    }

                    //if (Description.Contains("0x33"))
                    //{

                    //    FinalDesc = $": {sb}>\r\n";
                    //    OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 2, Convert.ToUInt32(sb.ToString()), false, "ushort"));
                    //}

                    else
                        FinalDesc = $": {sb}>";

            

                    break;
                case "byte":
                    for (int i = 0; i < ArgCount; i++)
                    {
                        var arg = br.ReadByte();
                        sb.Append($" {arg}");
                        Args.Add(arg);

                        if (i < ArgCount - 2)
                            sb.Append(" ");
                    }

                    if (br.BaseStream.Position >= 0xA95)
                    {

                    }

                    if (Description.Equals("<comand0x0D"))
                    {


                        if (Args[0] == 0xF4)
                        {
                            var secondCode = br.ReadUInt16();
                            OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 2, secondCode, false, "ushort"));
                            Args.Add(secondCode);
                            var thirdCode = br.ReadUInt16();
                            OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 2, thirdCode, false, "ushort"));
                            Args.Add(thirdCode);
                            sb.Append($" {secondCode} ");
                            sb.Append($"{thirdCode}");
                        }
                        else if (Args[0] == 0xF0)
                        {

                            var secondCode = br.ReadByte();
                            OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 1, secondCode, false, "byte"));
                            Args.Add(secondCode);

                            if (secondCode == 00)
                            {
                                Args.Add(0);
                                sb.Append(" 0");
                            }
                            else
                            {

                                OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 1, secondCode, false, "byte"));
                                var thirdCode = br.ReadByte();
                                Args.Add(thirdCode);
                                OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 1, thirdCode, false, "byte"));
                                sb.Append($" {secondCode}");
                                sb.Append($" {thirdCode}");
                                if (thirdCode >= 0x80)
                                {
                                    var fourthCode = br.ReadByte();
                                    OffsetsWithStrings.Add(new OffsetWithString((uint)br.BaseStream.Position - 1, fourthCode, false, "byte"));
                                    Args.Add(fourthCode);

                                    sb.Append($" {fourthCode}");
                                }


                            }
                        } 
                        else if (Args[0] == 0xF1) 
                        {
                        
                        }
                        else if (Args[0] == 0xF2)
                        {

                        }
                    }
              


                        FinalDesc = $":{sb}>";


                    break;

                default:
                    FinalDesc = ">";
                    break;

            }


        }
    }
}
