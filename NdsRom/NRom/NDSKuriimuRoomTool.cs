using plugin_nintendo.Archives;

namespace NdsRom.NRom;


public static class NDSKuriimuRoomTool
{
    public async static Task ExportRomWithKuriimu(string inputPath, string destPath)
    {
        //stream de leitura de arquivo

        var dest = $@"{destPath}";
        Directory.CreateDirectory(dest);

        using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
        {
            var nds = new Nds();
            var result = nds.Load(fs);

            foreach (var file in result)
            {

                if (file.FileSize > 0)
                {
                    var dirDest = $@"{dest}{Path.GetDirectoryName(file.FilePath.ToString())}";
                    var fileName = Path.GetFileName(file.FilePath.ToString());
                    Directory.CreateDirectory(dirDest);
                    var fileData = await file.GetFileData();
                    // convert fileData stream to byte array
                    var fileBytes = new byte[fileData.Length];
                    fileData.Read(fileBytes, 0, fileBytes.Length);
                    File.WriteAllBytes($@"{dirDest}\{fileName}", fileBytes);
                }
               

            }
        }

    }

    public static void ImportRomWithKuriimu(string originalRomPath, string modifiedFilesPath, string outputRomPath)
    {

        using (var fs = new FileStream(originalRomPath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
        {
            var nds = new Nds();
            var result = nds.Load(fs);
            foreach (var file in result)
            {
                var modifiedFilePath = $"{modifiedFilesPath}{file.FilePath.ToString().Replace(@"/", @"\")}" ;
                if (File.Exists(modifiedFilePath) && file.FileSize > 0)
                {
                    var fileBytes = File.ReadAllBytes(modifiedFilePath);
                    file.SetFileData(new MemoryStream(fileBytes));
                    
                }
            }
           
            using (var outputFs = new FileStream(outputRomPath, FileMode.Create, FileAccess.Write))
            {
                nds.Save(outputFs, result);
            }
        }
    }

    // compare old room with new room and show differences
    public static void CompareRoms(string oldRomPath, string newRomPath)
    {
        using (var oldFs = new FileStream(oldRomPath, FileMode.Open, FileAccess.Read))
        using (var newFs = new FileStream(newRomPath, FileMode.Open, FileAccess.Read))
        {
            var oldNds = new Nds();
            var newNds = new Nds();

            var oldFiles = oldNds.Load(oldFs);
            var newFiles = newNds.Load(newFs);

            var differences = new List<string>();

            foreach (var oldFile in oldFiles)
            {
                var matchingNewFile = newFiles.FirstOrDefault(f => f.FilePath.ToString() == oldFile.FilePath.ToString());

                if (matchingNewFile == null)
                {
                    differences.Add($"File missing in new ROM: {oldFile.FilePath}");
                    continue;
                }

                if (oldFile.FileSize != matchingNewFile.FileSize)
                {
                    differences.Add($"File size mismatch: {oldFile.FilePath} (Old: {oldFile.FileSize}, New: {matchingNewFile.FileSize})");
                }
                else
                {
                    using (var oldData = oldFile.GetFileData().Result)
                    using (var newData = matchingNewFile.GetFileData().Result)
                    {
                        if (!StreamsAreEqual(oldData, newData))
                        {
                            differences.Add($"File content mismatch: {oldFile.FilePath}");
                        }
                    }
                }
            }

            foreach (var newFile in newFiles)
            {
                var matchingOldFile = oldFiles.FirstOrDefault(f => f.FilePath.ToString() == newFile.FilePath.ToString());

                if (matchingOldFile == null)
                {
                    differences.Add($"New file added in new ROM: {newFile.FilePath}");
                }
            }

            if (differences.Count == 0)
            {
                Console.WriteLine("No differences found between the ROMs.");
            }
            else
            {
                Console.WriteLine("Differences found:");
                foreach (var difference in differences)
                {
                    Console.WriteLine(difference);
                }
            }
        }
    }

    private static bool StreamsAreEqual(Stream stream1, Stream stream2)
    {
        const int bufferSize = 1024 * 4;
        var buffer1 = new byte[bufferSize];
        var buffer2 = new byte[bufferSize];

        while (true)
        {
            var count1 = stream1.Read(buffer1, 0, buffer1.Length);
            var count2 = stream2.Read(buffer2, 0, buffer2.Length);

            if (count1 != count2)
            {
                return false;
            }

            if (count1 == 0)
            {
                return true;
            }

            if (!buffer1.AsSpan(0, count1).SequenceEqual(buffer2.AsSpan(0, count2)))
            {
                return false;
            }
        }
    }
}