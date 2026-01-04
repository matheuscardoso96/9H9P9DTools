using Lib999.Text;

namespace Lib999.Tests
{
    public static class FsbComparer
    {
        static void Compare()
        {
            var fsb1 = new FsbTexts(@"C:\Users\djmat\source\repos\9H9P9DTools\9H9P9DTools\bin\Debug\net6.0\999\root\scr\b11.fsb");
            //var fsb2 = new FsbTexts(@"C:\Users\djmat\source\repos\9H9P9DTools\9H9P9DTools\bin\Debug\net6.0\999_converted\999\root\scr\b11.fsb");
            var fsb2 = new FsbTexts(@"C:\desktop\999 projeto\testes\jp\scr\b11.fsb");

            var non0d1Commands = fsb1.MainStringBlock.EventDialogs.Where(x => x.Code != 0xD).ToList();
            var non0d2Commands = fsb2.MainStringBlock.EventDialogs.Where(x => x.Code != 0xD).ToList();

            //if (non0d1Commands.Count > non0d2Commands.Count)
            //{
            //    non0d1Commands = non0d1Commands.Take(non0d2Commands.Count).ToList();
            //}
            //else if (non0d2Commands.Count > non0d1Commands.Count)
            //{
            //    non0d2Commands = non0d2Commands.Take(non0d1Commands.Count).ToList();
            //}

            var non0d2CommandsCounter = 0;


            var diffList1 = new List<CommandEvent>();
            var diffList2 = new List<CommandEvent>();

            for (int i = 0; i < non0d1Commands.Count; i++)
            {
                if (non0d2CommandsCounter >= non0d2Commands.Count)
                {
                    diffList1.Add(non0d1Commands[i]);
                    break;
                }

                var command1 = non0d1Commands[i];
                var command2 = non0d2Commands[non0d2CommandsCounter];
                if (command1.Code == 0xD)
                {
                    if (command1.Args[0] == 0xF4)
                    {
                        continue;
                    }

                }

                if (command1.Code == 0x2f)
                {

                    continue;


                }

                //if (command1.Code == 0x33)
                //{

                //    continue;


                //}

                //if (command1.Code == 0x34)
                //{

                //    continue;


                //}

                if (command1.Code != command2.Code)
                {
                    i--;
                    non0d2CommandsCounter++;
                    continue;
                }

                var addedtoList = false;
                //compare args betewwen command1 and command2
                for (int j = 0; j < command1.Args.Count; j++)
                {

                    if (command1.Args[j] != command2.Args[j])
                    {
                        if (addedtoList == false)
                        {
                            diffList1.Add(command1);
                            diffList2.Add(command2);
                        }
                        addedtoList = true;
                        Console.WriteLine($"Difference found in offset c1 0x{command1.Offset.ToString("X")} offset c2 0x{command2.Offset.ToString("X")} command 0x{command1.Code.ToString("X")} arg {j}: {command1.Args[j]} != {command2.Args[j]}");
                    }
                    else
                    {


                    }
                }

                non0d2CommandsCounter++;
            }
        }
    }
}
