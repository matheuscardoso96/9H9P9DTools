using System.Text;

namespace Lib999.Text
{
    public class CommandEvent : ICloneable
    {
        public string Description { get; set; } = "";
        public int ArgCount { get; set; }
        public string ArgType { get; set; } = "";
        public string FinalDesc { get; set; } = "";
        public int[] Args { get; set; } = Array.Empty<int>();

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
            Args = new int[ArgCount];
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
                    if (Description.Equals("<comand0x37"))
                    {

                    }

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
                        if (Args[0] == 2400)
                        {

                        }

                        if (Args[0] == 0xF4)
                        {
                            sb.Append($" {br.ReadUInt16()} ");
                            sb.Append($"{br.ReadUInt16()}");

                            if (FinalDesc.Contains("2400"))
                            {

                            }
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

                            if (FinalDesc.Contains("2400"))
                            {

                            }
                        }
                    }

                    
                     FinalDesc = $":{sb}>";

                    if (FinalDesc.Contains("2400"))
                    {

                    }


                    break;

                default:
                        FinalDesc = ">";
                    break;

            }

            
        }

        
    }
}
