using System;
using System.IO;
using System.Text.Json;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool;

public static class Archive
{
    public static int HandleExtract(FileInfo filename, DirectoryInfo outputFolder)
    {
        try
        {
            var path = filename.ToString();
            if (WebBundleHelper.IsWebBundle(path))
            {
                WebBundleHelper.Extract(filename, outputFolder);
            }
            else if (ArchiveDetector.IsUnityArchive(path))
            {
                ExtractAssetBundle(filename, outputFolder);
            }
            else
            {
                Console.Error.WriteLine("File is not a supported archive type.");
                return 1;
            }
        }
        catch (Exception err) when (
            err is NotSupportedException
            || err is FileFormatException)
        {
            Console.Error.WriteLine("Error opening archive");
            Console.Error.WriteLine(err.Message);
            return 1;
        }
        return 0;
    }

    public static int HandleList(FileInfo filename, OutputFormat format)
    {
        try
        {
            var path = filename.ToString();
            if (WebBundleHelper.IsWebBundle(path))
            {
                WebBundleHelper.List(filename, format);
            }
            else if (ArchiveDetector.IsUnityArchive(path))
            {
                ListAssetBundle(filename, format);
            }
            else
            {
                Console.Error.WriteLine("File is not a supported archive type.");
                return 1;
            }
        }
        catch (Exception err) when (
            err is NotSupportedException
            || err is FileFormatException)
        {
            Console.Error.WriteLine("Error opening archive");
            Console.Error.WriteLine(err.Message);
            return 1;
        }

        return 0;
    }

    static void ExtractAssetBundle(FileInfo filename, DirectoryInfo outputFolder)
    {
        Console.WriteLine($"Extracting asset bundle: {filename}");
        using var archive = UnityFileSystem.MountArchive(filename.FullName, "/");
        foreach (var node in archive.Nodes)
        {
            Console.WriteLine($"... Extracting {node.Path}");
            CopyFile("/" + node.Path, Path.Combine(outputFolder.FullName, node.Path));
        }
    }

    static void ListAssetBundle(FileInfo filename, OutputFormat format)
    {
        using var archive = UnityFileSystem.MountArchive(filename.FullName, "/");

        if (format == OutputFormat.Json)
        {
            var jsonArray = new object[archive.Nodes.Count];
            for (int i = 0; i < archive.Nodes.Count; i++)
            {
                var node = archive.Nodes[i];
                jsonArray[i] = new { path = node.Path, size = node.Size, flags = node.Flags.ToString() };
            }
            var json = JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            foreach (var node in archive.Nodes)
            {
                Console.WriteLine($"{node.Path}");
                Console.WriteLine($"  Size: {node.Size}");
                Console.WriteLine($"  Flags: {node.Flags}");
                Console.WriteLine();
            }
        }
    }

    static void CopyFile(string source, string dest)
    {
        using var sourceFile = UnityFileSystem.OpenFile(source);
        // Create the containing directory if it doesn't exist.
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        using var destFile = new FileStream(dest, FileMode.Create);

        const int blockSize = 256 * 1024;
        var buffer = new byte[blockSize];
        long actualSize;

        do
        {
            actualSize = sourceFile.Read(blockSize, buffer);
            destFile.Write(buffer, 0, (int)actualSize);
        }
        while (actualSize == blockSize);
    }
}
