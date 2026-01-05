using Lib999.Font;
using Lib999.Image;
using Lib999.Text;
using NdsRom.NRom;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length > 0)
{

    if (args.Length == 3 && args[0] == "-e" && args[1] == "-r" && args[2].Contains(".nds"))
    {
       await ExportFiles(args[2]);
    }
    else if (args.Length == 3 && args[0] == "-i" && args[1] == "-r" && args[2].Contains(".nds"))
    {
        ImportFiles(args[2]);
    }

    Console.WriteLine("Fim da operação, aperte qualquer letra para encerrar.");
    Console.ReadKey();
}



async static Task ExportFiles(string romPath)
{
    if (!Directory.Exists("999") || Directory.GetFiles("999", "*", SearchOption.AllDirectories).Length == 0)
    {
        Console.WriteLine("Exportando rom para a pasta 999...");
        await NDSKuriimuRoomTool.ExportRomWithKuriimu(romPath, @"999\root");
    }
   
    var exportArgs = File.ReadAllLines(@"EssentialFiles\fileExportList.txt").ToList();

    foreach (var file in exportArgs)
    {

        if (file.Contains("*"))
            continue;

        if (file.Contains("-fe"))
            ExportFont(file);

        if (file.Contains("-bge"))
            ExportBg(file);

        if (file.Contains("-fsbe"))
            ExportFsb(file);

        if (file.Contains("-dattextv1e"))
            ExportFileTexts(file);

        if (file.Contains("-dattextv4e"))
            ExportSystemTexts(file);

        if (file.Contains("-itemstextse"))
            ExportItemsNames(file);

        if (file.Contains("-cameratextse"))
            ExportCameraTexts(file);

        if (file.Contains("-charatextse"))
            ExportCharaTexts(file);
    }

    var destDir = "999_edited";
    Directory.CreateDirectory(destDir);
}

const string filesToImportDir = "999_edited\\";

static void ImportFiles(string romPath)
{

    if (!File.Exists(romPath))
    {
        Console.WriteLine($"Rom não econtrada. Caminho {romPath}, vefique o arquivo importação .bat.");
        return;
    }
    
    var importArgs = File.ReadAllLines(@"EssentialFiles\fileExportList.txt");
    var files = Directory.GetFiles(filesToImportDir, "*", SearchOption.AllDirectories);


    foreach (var file in files)
    {
        if (file.Contains(".png") && file.Contains("kanji"))
            continue;

        var fileP = file.Replace(".png", "").Replace(".txt", "").Split(new string[] { filesToImportDir }, StringSplitOptions.RemoveEmptyEntries)[0];
        var arg = importArgs.FirstOrDefault(x => x.Contains(fileP));

        if (arg is null)
            continue;

        if (arg.Contains("-bge"))
            arg = arg.Replace("-bge", "-bgi");

        if (arg.Contains("-fe"))
            arg = arg.Replace("-fe", "-fi");

        if (arg.Contains("-fsbe"))
            arg = arg.Replace("-fsbe", "-fsbi");

        if (arg.Contains("-dattextv1e"))
            arg = arg.Replace("-dattextv1e", "-dattextv1i");

        if (arg.Contains("-dattextv4e"))
            arg = arg.Replace("-dattextv4e", "-dattextv4i");

        if (arg.Contains("-itemstextse"))
            arg = arg.Replace("-itemstextse", "-itemstextsi");

        if (arg.Contains("-cameratextse"))
            arg = arg.Replace("-cameratextse", "-cameratextsi");

        if (arg.Contains("-charatextse"))
            arg = arg.Replace("-charatextse", "-charatextsi");

        if (arg.Contains("*"))
            continue;

        if (arg.Contains("-fi"))
            ImportFont(arg, file);

        if (arg.Contains("-bgi"))
            ImportBg(arg, file);

        if (arg.Contains("-fsbi"))
            ImportFsb(arg, file);

        if (arg.Contains("-dattextv1i"))
            ImportFileTexts(arg, file);

        if (arg.Contains("-dattextv4i"))
            ImportSystemTexts(arg, file);

        if (arg.Contains("-itemstextsi"))
            ImportItemsNames(arg, file);

        if (arg.Contains("-cameratextsi"))
            ImportCameraTexts(arg, file);

        if (arg.Contains("-charatextsi"))
            ImportCharaTexts(arg, file);
    }


    var convertedDir = "999_converted";

    if (!Directory.Exists(convertedDir))
    {
        Console.WriteLine("Não foi encontrado arquivos para importar em 999_converted.");
        return;
    }


    var filesToReplace = Directory.GetFiles(convertedDir, "*", SearchOption.AllDirectories);

    foreach (var file in filesToReplace)
    {
        
        var originalPath = Path.GetRelativePath(convertedDir, file);

        if (File.Exists(originalPath))
        {
            File.Copy(file, originalPath, overwrite: true);
            Console.WriteLine($"Replaced: {originalPath}");
        }
        else
        {
            Console.WriteLine($"Arquivo não encontrado no diretório original: {originalPath}");
        }
    }


    var newRomName = Path.GetFileName(romPath).Replace(".nds", $"_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}.nds");

    NDSKuriimuRoomTool.ImportRomWithKuriimu(romPath, $@"999\root", newRomName);
}

static void ExportFont(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var font = new SirFont(argsSplit[0]);
    Console.WriteLine($"Exportando fonte: {font.FontName}");
    font.ExportFont(argsSplit[0]);
}

static void ImportFont(string args, string tableTxtPath)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var font = new SirFont(tableTxtPath, tableTxtPath.Replace(".txt", ".png"));
    font.SaveSirFont(argsSplit[0]);
    Console.WriteLine($"Importando fonte: {font.FontName}");

}

static void ExportBg(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');

    var bg = new SirBg(argsSplit[0], argsSplit.Any(x => x.Contains("expD")));
    Console.WriteLine($"Exportando bg: {bg.FileName}");
    bg.SirBgToPng(argsSplit[0]);

}

static void ImportBg(string args, string pngPath)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var bg = new SirBg(argsSplit[0]);
    Console.WriteLine($"Importando bg: {bg.FileName}");
    bg.PngToSirBg(pngPath, argsSplit[0]);
}

static void ExportFsb(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    if (File.Exists(argsSplit[0]))
    {
        Console.WriteLine($"Exportando fsb: {Path.GetFileName(argsSplit[0])}");
        var texts = new FsbTexts(argsSplit[0]);
        texts.FsbToTxt(argsSplit[0], false);

    }

}

static void ImportFsb(string args, string txtfilePath)
{
    var argsSplit = args.Replace(" ", "").Split(',');

    try
    {

        Console.WriteLine($"Importando fsb: {Path.GetFileName(argsSplit[0])}");
        var texts = new FsbTexts(argsSplit[0], txtfilePath);
        texts.TxtToFsb(argsSplit[0], txtfilePath);

    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Falha ao converter texto anterior para bytes.\r\nArquivo: {Path.GetFileName(txtfilePath)}\r\nErro: {ex.Message}");
        Console.WriteLine("Pressione Enter para continuar.");
        Console.ForegroundColor = ConsoleColor.White;
    }

}

static void ExportFileTexts(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var texts = new FileTexts(argsSplit[0]);
    Console.WriteLine($"Exportando file text: {Path.GetFileName(argsSplit[0])}");
}

static void ImportFileTexts(string args, string txtfilePath)
{
    var argsSplit = args.Replace(" ", "").Split(',');

    try
    {
        
        var texts = new FileTexts(argsSplit[0], txtfilePath);
        Console.WriteLine($"Importando file text: {Path.GetFileName(argsSplit[0])}");
    }
    catch
    {

        Console.WriteLine($"Falha ao converter arquivo: {Path.GetFileName(argsSplit[0])}");
    }
   
}

static void ExportSystemTexts(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var texts = new SystemTexts(argsSplit[0]);
    Console.WriteLine($"Exportando SirTextsV4: {Path.GetFileName(argsSplit[0])}");
}

static void ImportSystemTexts(string args, string txtfilePath)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    try
    {
        
        var texts = new SystemTexts(argsSplit[0], txtfilePath);
        Console.WriteLine($"Importando SirTextsV4: {Path.GetFileName(argsSplit[0])}");
    }
    catch (Exception)
    {

        Console.WriteLine($"Falha ao converter arquivo: {Path.GetFileName(argsSplit[0])}");
    }
   
}

static void ExportItemsNames(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var texts = new ItemsNames(argsSplit[0]);
    Console.WriteLine($"Exportando Nomes de Itens: {Path.GetFileName(argsSplit[0])}");
}

static void ImportItemsNames(string args, string txtfilePath)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    
    try
    {
        
        var texts = new ItemsNames(argsSplit[0], txtfilePath);
        Console.WriteLine($"Importando Nomes de Itens: {Path.GetFileName(argsSplit[0])}");
    }
    catch (Exception)
    {

        Console.WriteLine($"Falha ao converter arquivo: {Path.GetFileName(argsSplit[0])}");
    }
    
}

static void ExportCameraTexts(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var texts = new CameraTexts(argsSplit[0]);
    Console.WriteLine($"Exportando Textos de Câmera: {Path.GetFileName(argsSplit[0])}");
}

static void ImportCameraTexts(string args, string txtfilePath)
{
    var argsSplit = args.Replace(" ", "").Split(',');

    try
    {
        
        var texts = new CameraTexts(argsSplit[0], txtfilePath);
        Console.WriteLine($"Importando Textos de Câmera: {Path.GetFileName(argsSplit[0])}");
    }
    catch (Exception)
    {

        Console.WriteLine($"Falha ao converter arquivo: {Path.GetFileName(argsSplit[0])}");
    }
    
}

static void ExportCharaTexts(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var texts = new CharaTexts(argsSplit[0]);
    Console.WriteLine($"Exportando Textos de Chara: {Path.GetFileName(argsSplit[0])}");
}

static void ImportCharaTexts(string args, string txtfilePath)
{
    var argsSplit = args.Replace(" ", "").Split(',');

    try
    {
        var texts = new CharaTexts(argsSplit[0], txtfilePath);
        Console.WriteLine($"Importando Textos de Chara: {Path.GetFileName(argsSplit[0])}");
    }
    catch (Exception)
    {

        Console.WriteLine($"Falha ao converter arquivo: {Path.GetFileName(argsSplit[0])}");
    }
    
}

