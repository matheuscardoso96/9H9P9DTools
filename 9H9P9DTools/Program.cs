// See https://aka.ms/new-console-template for more information
using Lib999.Font;
using Lib999.Image;
using Lib999.Text;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length > 0)
{

    if (args[0] == "-e")
    {
        ExportFiles();
    }
    else if (args[0] == "-i")
    {

    }

    Console.WriteLine("Fim da operação.");
    Console.ReadKey();
}



static void ExportFiles()
{

    var exportArgs = File.ReadAllLines("fileExportList.txt").ToList();

    var b11Path = @"999\root\scr\b11_jp.fsb";
    exportArgs.Add($"{b11Path},-fsbe");


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

}

const string filesToImportDir = "999_edited\\";

static void ImportFiles()
{
    var importArgs = File.ReadAllLines("fileExportList.txt");
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
    var img = bg.ConvertImageToBmp();
    var dest = $"999_exported\\{argsSplit[0].Replace(Path.GetFileName(argsSplit[0]), "")}";
    Directory.CreateDirectory(dest);
    img.Save($"{dest}\\{bg.FileName}.png");

}

static void ImportBg(string args, string pngPath)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var bg = new SirBg(argsSplit[0]);
    Console.WriteLine($"Importando bg: {bg.FileName}");
    bg.InsertImage(pngPath, argsSplit[0]);
}

static void ExportFsb(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    if (File.Exists(argsSplit[0]))
    {
        Console.WriteLine($"Exportando fsb: {Path.GetFileName(argsSplit[0])}");
        var texts = new FsbTexts(argsSplit[0]);
        texts.FsbToTxt(argsSplit[0], true);

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
        Console.ReadKey();

    }
    finally
    {

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
    var texts = new FileTexts(argsSplit[0], txtfilePath);
    Console.WriteLine($"Importando file text: {Path.GetFileName(argsSplit[0])}");
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
    var texts = new SystemTexts(argsSplit[0], txtfilePath);
    Console.WriteLine($"Importando SirTextsV4: {Path.GetFileName(argsSplit[0])}");
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
    var texts = new ItemsNames(argsSplit[0], txtfilePath);
    Console.WriteLine($"Importando Nomes de Itens: {Path.GetFileName(argsSplit[0])}");
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
    var texts = new CameraTexts(argsSplit[0], txtfilePath);
    Console.WriteLine($"Importando Textos de Câmera: {Path.GetFileName(argsSplit[0])}");
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
    var texts = new CharaTexts(argsSplit[0], txtfilePath);
    Console.WriteLine($"Importando Textos de Chara: {Path.GetFileName(argsSplit[0])}");
}

