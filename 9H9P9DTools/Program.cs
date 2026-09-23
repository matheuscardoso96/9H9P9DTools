using Lib999.Font;
using Lib999.Image;
using Lib999.Text;
using LibDeImagensGbaDs.Conversor;
using NdsRom.NRom;

Console.OutputEncoding = System.Text.Encoding.UTF8;

//args = new string[] { "-i", "-r", "Nine Hours, Nine Persons, Nine Doors (USA).nds", "" }; 
//args = new string[] { "-e", "-r", "Nine Hours, Nine Persons, Nine Doors (USA).nds", "-parallel" }; 

if (args.Length > 0)
{
    bool useParallel = args.Contains("-parallel");

    if (args.Length >= 3 && args[0] == "-e" && args[1] == "-r" && args[2].Contains(".nds"))
    {
       await ExportFiles(args[2]);
    }
    else if (args.Length >= 3 && args[0] == "-i" && args[1] == "-r" && args[2].Contains(".nds"))
    {
        await ImportFiles(args[2], useParallel);
    }

    Console.WriteLine("Fim da operação, aperte qualquer letra para encerrar.");
    Console.ReadKey();
}

async static Task ExportFilesOld(string romPath)
{
    if (!Directory.Exists("999") || Directory.GetFiles("999", "*", SearchOption.AllDirectories).Length == 0)
    {
        Console.WriteLine("Exportando rom para a pasta 999...");
        await NDSKuriimuRoomTool.ExportRomWithKuriimu(romPath, @"999\root");
    }
   
    var exportArgs = File.ReadAllLines(@"EssentialFiles\fileExportList.txt").ToList();

    foreach (var file in exportArgs)
    {

        var fileExport = $"c_{file}";

        if (fileExport.Contains("*"))
            continue;

        if (fileExport.Contains("-fe"))
            ExportFont(fileExport);

        if (fileExport.Contains("-bge"))
            ExportBg(fileExport);

        if (fileExport.Contains("-fsbe"))
            ExportFsb(fileExport);

        if (fileExport.Contains("-dattextv1e"))
            ExportFileTexts(fileExport);

        if (fileExport.Contains("-dattextv4e"))
            ExportSystemTexts(fileExport);

        if (fileExport.Contains("-itemstextse"))
            ExportItemsNames(fileExport);

        if (fileExport.Contains("-cameratextse"))
            ExportCameraTexts(fileExport);

        if (fileExport.Contains("-charatextse"))
            ExportCharaTexts(fileExport);
    }

    var destDir = "999_edited\\root";
    Directory.CreateDirectory(destDir);
}

async static Task ExportFiles(string romPath)
{
    if (!Directory.Exists("999") || Directory.GetFiles("999", "*", SearchOption.AllDirectories).Length == 0)
    {
        Console.WriteLine("Exportando rom para a pasta 999...");
        await NDSKuriimuRoomTool.ExportRomWithKuriimu(romPath, @"999\root");
    }

    var exportArgs = File.ReadAllLines(@"EssentialFiles\fileExportList.txt").ToList();
    var preparedExportArgs = exportArgs.Select(file => $"c_{file}").ToList();

    // BGs automáticos.
   var bgScanExport = BgResourceAutoExporter.Export(@"c_999\root\bg");

    // Fontes + textos automáticos.
   var automaticResources = AutomaticProjectResourceCatalog.Discover(@"c_999");

    var automaticExport =
        new AutomaticProjectResourceExportResult();

    foreach (var resource in automaticResources)
    {
        try
        {
            string arg = resource.CreateExportArgument();

            switch (resource.Kind)
            {
                case AutomaticProjectResourceKind.Font:
                    ExportFont(arg);
                    break;

                case AutomaticProjectResourceKind.HistoryFsb:
                    ExportFsb(arg);
                    break;

                case AutomaticProjectResourceKind.FileTexts:
                    ExportFileTexts(arg);
                    break;

                case AutomaticProjectResourceKind.SystemTexts:
                    ExportSystemTexts(arg);
                    break;

                case AutomaticProjectResourceKind.ItemsNames:
                    ExportItemsNames(arg);
                    break;

                case AutomaticProjectResourceKind.CameraTexts:
                    ExportCameraTexts(arg);
                    break;

                case AutomaticProjectResourceKind.CharaTexts:
                    ExportCharaTexts(arg);
                    break;


                case AutomaticProjectResourceKind.RoomTexts:
                    ExportRoomTexts(arg);
                    break;

                case AutomaticProjectResourceKind.StaffTexts:
                    ExportRoomTexts(arg);
                    break;
            }

            automaticExport.AddExported(resource);
        }
        catch (Exception ex)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;

            Console.WriteLine(
                $"Falha na exportação automática de '{resource.SourcePath}': " +
                $"{ex.Message}. A fileExportList poderá tentar o fallback.");

            Console.ForegroundColor = oldColor;
        }
    }

    Console.WriteLine(
        $"Scan automático concluído: " +
        $"{bgScanExport.ExportedCount} BG(s), " +
        $"{automaticExport.ExportedCount} fonte(s)/texto(s).");

    // A lista fica apenas como fallback.
    // Itens já processados são ignorados silenciosamente.
    foreach (var fileExport in preparedExportArgs)
    {
        if (fileExport.Contains("*"))
            continue;

        if (BgResourceAutoExporter.IsActionFldBgEntry(fileExport) &&
            bgScanExport.IsProcessedActionFld(fileExport))
        {
            continue;
        }

        if (bgScanExport.WasExported(fileExport))
            continue;

        if (automaticExport.WasExported(fileExport))
            continue;

        if (fileExport.Contains("-fe"))
            ExportFont(fileExport);

        if (fileExport.Contains("-bge"))
            ExportBg(fileExport);

        if (fileExport.Contains("-fsbe"))
            ExportFsb(fileExport);

        if (fileExport.Contains("-dattextv1e"))
            ExportFileTexts(fileExport);

        if (fileExport.Contains("-dattextv4e"))
            ExportSystemTexts(fileExport);

        if (fileExport.Contains("-itemstextse"))
            ExportItemsNames(fileExport);

        if (fileExport.Contains("-cameratextse"))
            ExportCameraTexts(fileExport);

        if (fileExport.Contains("-charatextse"))
            ExportCharaTexts(fileExport);
    }

    var destDir = "999_edited\\root";
    Directory.CreateDirectory(destDir);
}



const string filesToImportDir = "999_edited\\";

async static Task ImportFiles(string romPath, bool useParallel)
{
    ImageDsConverter.InitilizeConverters();

    if (!File.Exists(romPath))
    {
        Console.WriteLine($"Rom não encontrada. Caminho {romPath}, verifique o arquivo de importação .bat.");
        return;
    }

    // 999_converted é uma pasta temporária desta execução.
    // Limpar antes evita reaplicar arquivos gerados por imports anteriores,
    // inclusive arquivos antigos com nomes incorretos contendo @W@H@BPP.
    var convertedDir = "999_converted";

    if (Directory.Exists(convertedDir))
        Directory.Delete(convertedDir, recursive: true);

    var importArgs =
        File.ReadAllLines(@"EssentialFiles\fileExportList.txt");

    var bgImportIndex =
        BgResourceAutoImporter.Build(@"c_999\root\bg");

    var automaticProjectImportIndex =
        AutomaticProjectResourceCatalog.BuildImportIndex(@"c_999");

    Console.WriteLine(
        $"Índices automáticos de importação: " +
        $"{bgImportIndex.Count} BG(s), " +
        $"{automaticProjectImportIndex.Count} fonte(s)/texto(s).");

    // Falhas ficam acumuladas durante toda a operação e são exibidas
    // somente no final para não poluir o log durante a importação.
    var importErrors =
        new System.Collections.Concurrent.ConcurrentQueue<string>();

    // Continua processando SOMENTE o que existir em 999_edited.
    var files =
        Directory.GetFiles(
            filesToImportDir,
            "*",
            SearchOption.AllDirectories);

    if (useParallel)
    {
        await Task.Run(() =>
        {
            Parallel.ForEach(
                files,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism =
                        Math.Max(1, (int)(Environment.ProcessorCount * 0.2))
                },
                file =>
                {
                    ProcessFile(
                        file,
                        importArgs,
                        bgImportIndex,
                        automaticProjectImportIndex,
                        importErrors);
                });
        });
    }
    else
    {
        foreach (var file in files)
        {
            ProcessFile(
                file,
                importArgs,
                bgImportIndex,
                automaticProjectImportIndex,
                importErrors);
        }
    }

    if (!Directory.Exists(convertedDir))
    {
        Console.WriteLine("Não foram encontrados arquivos para importar em 999_converted.");
        PrintImportErrors(importErrors);
        return;
    }

    var filesToReplace =
        Directory.GetFiles(convertedDir, "*", SearchOption.AllDirectories);

    foreach (var file in filesToReplace)
    {
        var originalPath =
            Path.GetRelativePath(convertedDir, file);

        if (originalPath.StartsWith("c_"))
            originalPath = originalPath[2..];

        if (File.Exists(originalPath))
        {
            File.Copy(file, originalPath, overwrite: true);
            Console.WriteLine($"Replaced: {originalPath}");
        }
        else
        {
            importErrors.Enqueue(
                $"Arquivo não encontrado no diretório original: {originalPath}");
        }
    }

    var newRomName =
        Path.GetFileName(romPath).Replace(
            ".nds",
            $"_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}.nds");

    NDSKuriimuRoomTool.ImportRomWithKuriimu(
        romPath,
        $"999\\root",
        newRomName);

    // Último output da importação: resumo das falhas.
    PrintImportErrors(importErrors);
}

static void PrintImportErrors(
    System.Collections.Concurrent.ConcurrentQueue<string> importErrors)
{
    if (importErrors.IsEmpty)
        return;

    var oldColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Yellow;

    Console.WriteLine();
    Console.WriteLine($"Falhas de importação ({importErrors.Count}):");

    while (importErrors.TryDequeue(out var error))
        Console.WriteLine($"- {error}");

    Console.ForegroundColor = oldColor;
}

static void ProcessFile(
    string file,
    string[] importArgs,
    BgResourceImportIndex bgImportIndex,
    AutomaticProjectResourceImportIndex automaticProjectImportIndex,
    System.Collections.Concurrent.ConcurrentQueue<string> importErrors)
{
    // O PNG da fonte é usado junto com o TXT pelo ImportFont.
    if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
        Path.GetFileName(file).StartsWith(
            "kanji_",
            StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    string? automaticBgError = null;

    // BG automático.
    if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
        bgImportIndex.TryGetByEditedFile(
            file,
            filesToImportDir,
            out var bgResource))
    {
        try
        {
            var bg = new SirBg(bgResource.SourceDatPath);

            string sourceLabel =
                bgResource.SourceKind == BgImportSourceKind.ActionFld
                    ? "action.fld"
                    : "scan sem FLD";

            Console.WriteLine(
                $"Importando bg via {sourceLabel}: {bg.FileName} " +
                $"({bg.Width}x{bg.Height}, {bg.ColorDep} bpp)");

            bool converted = bg.PngToSirBg(
                file,
                bgResource.SourceDatPath);

            if (converted)
                return;

            automaticBgError =
                $"Falha ao converter BG '{Path.GetFileName(file)}': " +
                $"{bg.LastConversionError ?? "o tamanho do bitmap convertido não corresponde ao bloco gráfico original."}";
        }
        catch (Exception ex)
        {
            automaticBgError =
                $"Falha ao importar automaticamente '{file}' pelo scan de BG: " +
                $"{ex.Message}";
        }
    }

    if (file.Contains("room.dat.txt"))
    {

    }

    // Fonte/texto automático.
    if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
        automaticProjectImportIndex.TryGetByEditedFile(
            file,
            filesToImportDir,
            out var automaticResource))
    {
        string automaticArg =
            automaticResource.CreateImportArgument();

        switch (automaticResource.Kind)
        {
            case AutomaticProjectResourceKind.Font:
                ImportFont(automaticArg, file);
                break;

            case AutomaticProjectResourceKind.HistoryFsb:
                ImportFsb(automaticArg, file);
                break;

            case AutomaticProjectResourceKind.FileTexts:
                ImportFileTexts(automaticArg, file);
                break;

            case AutomaticProjectResourceKind.SystemTexts:
                ImportSystemTexts(automaticArg, file);
                break;

            case AutomaticProjectResourceKind.ItemsNames:
                ImportItemsNames(automaticArg, file);
                break;

            case AutomaticProjectResourceKind.CameraTexts:
                ImportCameraTexts(automaticArg, file);
                break;

            case AutomaticProjectResourceKind.CharaTexts:
                ImportCharaTexts(automaticArg, file);
                break;

            case AutomaticProjectResourceKind.RoomTexts:
                ImportRoomTexts(automaticArg, file);
                break;

            case AutomaticProjectResourceKind.StaffTexts:
                ImportRoomTexts(automaticArg, file);
                break;
        }

        return;
    }

    // Fallback antigo da lista.
    var fileP = file
        .Replace(".png", "")
        .Replace(".txt", "")
        .Split(
            new string[] { filesToImportDir },
            StringSplitOptions.RemoveEmptyEntries)[0];

    var arg =
        importArgs.FirstOrDefault(x => x.Contains(fileP));

    if (arg is null)
    {
        if (automaticBgError is not null)
            importErrors.Enqueue(automaticBgError);

        return;
    }

    arg = arg.Replace(@"999\", @"c_999\");

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

    if (arg.Contains("-romtextse"))
        arg = arg.Replace("-romtextse", "-romtextsi");

    if (arg.Contains("*"))
        return;

    if (arg.Contains("-fi"))
        ImportFont(arg, file);

    if (arg.Contains("-bgi"))
    {
        if (!ImportBg(arg, file, out var bgImportError))
        {
            importErrors.Enqueue(
                bgImportError ??
                automaticBgError ??
                $"Falha ao importar BG '{Path.GetFileName(file)}'.");
        }
    }
    else if (automaticBgError is not null)
    {
        // Havia falha no caminho automático, mas a entrada encontrada na lista
        // não era uma importação de BG.
        importErrors.Enqueue(automaticBgError);
    }

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

    if (arg.Contains("-romtextsi"))
        ImportRoomTexts(arg, file);
}






//async static Task ImportFilesOld(string romPath, bool useParallel)
//{
//    ImageDsConverter.InitilizeConverters();

//    if (!File.Exists(romPath))
//    {
//        Console.WriteLine($"Rom não encontrada. Caminho {romPath}, verifique o arquivo de importação .bat.");
//        return;
//    }

//    var importArgs = File.ReadAllLines(@"EssentialFiles\fileExportList.txt");
//    var files = Directory.GetFiles(filesToImportDir, "*", SearchOption.AllDirectories);

//    if (useParallel)
//    {
//        await Task.Run(() =>
//        {
//            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, (int)(Environment.ProcessorCount * 0.2)) }, file =>
//            {
//                ProcessFile(file, importArgs);
//            });
//        });
//    }
//    else
//    {
//        foreach (var file in files)
//        {
//            ProcessFile(file, importArgs);
//        }
//    }

//    var convertedDir = "999_converted";

//    if (!Directory.Exists(convertedDir))
//    {
//        Console.WriteLine("Não foram encontrados arquivos para importar em 999_converted.");
//        return;
//    }

//    var filesToReplace = Directory.GetFiles(convertedDir, "*", SearchOption.AllDirectories);

//    foreach (var file in filesToReplace)
//    {
//        var originalPath = Path.GetRelativePath(convertedDir, file);

//        if (originalPath.StartsWith("c_"))
//        {
//            originalPath = originalPath[2..];
//        }

//        if (File.Exists(originalPath))
//        {
//            File.Copy(file, originalPath, overwrite: true);
//            Console.WriteLine($"Replaced: {originalPath}");
//        }
//        else
//        {
//            Console.WriteLine($"Arquivo não encontrado no diretório original: {originalPath}");
//        }
//    }

//    var newRomName = Path.GetFileName(romPath).Replace(".nds", $"_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}.nds");

//    NDSKuriimuRoomTool.ImportRomWithKuriimu(romPath, $"999\\root", newRomName);
//}

//static void ProcessFileOld(string file, string[] importArgs)
//{
    

//    if (file.Contains(".png") && file.Contains("kanji"))
//        return;

//    var fileP = file.Replace(".png", "").Replace(".txt", "").Split(new string[] { filesToImportDir }, StringSplitOptions.RemoveEmptyEntries)[0];
//    var arg = importArgs.FirstOrDefault(x => x.Contains(fileP));

//    if (arg is null)
//        return;

//    arg = arg.Replace(@"999\",@"c_999\");

//    if (arg.Contains("-bge"))
//        arg = arg.Replace("-bge", "-bgi");

//    if (arg.Contains("-fe"))
//        arg = arg.Replace("-fe", "-fi");

//    if (arg.Contains("-fsbe"))
//        arg = arg.Replace("-fsbe", "-fsbi");

//    if (arg.Contains("-dattextv1e"))
//        arg = arg.Replace("-dattextv1e", "-dattextv1i");

//    if (arg.Contains("-dattextv4e"))
//        arg = arg.Replace("-dattextv4e", "-dattextv4i");

//    if (arg.Contains("-itemstextse"))
//        arg = arg.Replace("-itemstextse", "-itemstextsi");

//    if (arg.Contains("-cameratextse"))
//        arg = arg.Replace("-cameratextse", "-cameratextsi");

//    if (arg.Contains("-charatextse"))
//        arg = arg.Replace("-charatextse", "-charatextsi");

//    if (arg.Contains("*"))
//        return;

//    if (arg.Contains("-fi"))
//        ImportFont(arg, file);

//    if (arg.Contains("-bgi"))
//        ImportBg(arg, file);

//    if (arg.Contains("-fsbi"))
//        ImportFsb(arg, file);

//    if (arg.Contains("-dattextv1i"))
//        ImportFileTexts(arg, file);

//    if (arg.Contains("-dattextv4i"))
//        ImportSystemTexts(arg, file);

//    if (arg.Contains("-itemstextsi"))
//        ImportItemsNames(arg, file);

//    if (arg.Contains("-cameratextsi"))
//        ImportCameraTexts(arg, file);

//    if (arg.Contains("-charatextsi"))
//        ImportCharaTexts(arg, file);
//}

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

static bool ImportBg(
    string args,
    string pngPath,
    out string? error)
{
    error = null;

    try
    {
        var argsSplit = args.Replace(" ", "").Split(',');
        var bg = new SirBg(argsSplit[0]);

        Console.WriteLine($"Importando bg: {bg.FileName}");

        bool converted = bg.PngToSirBg(
            pngPath,
            argsSplit[0]);

        if (converted)
            return true;

        error =
            $"Falha ao converter BG '{Path.GetFileName(pngPath)}': " +
            $"{bg.LastConversionError ?? "o tamanho do bitmap convertido não corresponde ao bloco gráfico original."}";

        return false;
    }
    catch (Exception ex)
    {
        error =
            $"Falha ao importar BG '{Path.GetFileName(pngPath)}': {ex.Message}";

        return false;
    }
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

static void ExportRoomTexts(string args)
{
    var argsSplit = args.Replace(" ", "").Split(',');
    var texts = new RoomTexts(argsSplit[0]);
    texts.RomTextstoTxt(argsSplit[0]);
    Console.WriteLine($"Exportando Textos de Room: {Path.GetFileName(argsSplit[0])}");
}

static void ImportRoomTexts(string args, string txtfilePath)
{
    var argsSplit = args.Replace(" ", "").Split(',');

    try
    {
        var texts = new RoomTexts(argsSplit[0]);
        texts.RoomTextsToDat(argsSplit[0], txtfilePath);
        Console.WriteLine($"Importando Textos de RoomTexts: {Path.GetFileName(argsSplit[0])}");
    }
    catch (Exception)
    {

        Console.WriteLine($"Falha ao converter arquivo: {Path.GetFileName(argsSplit[0])}");
    }

}

