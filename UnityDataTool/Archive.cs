using System;
using System.Collections.Generic;
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

    public static int HandleHeader(FileInfo filename, OutputFormat format)
    {
        var path = filename.ToString();

        if (WebBundleHelper.IsWebBundle(path))
        {
            Console.Error.WriteLine("Web bundle files (.data, .data.gz, .data.br) use a different format. The header command is only supported for Unity Archive files.");
            return 1;
        }

        if (!ArchiveDetector.TryReadArchiveHeader(filename.FullName, out var info, out var errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        if (format == OutputFormat.Json)
            OutputHeaderJson(info);
        else
            OutputHeaderText(info);

        return 0;
    }

    static void OutputHeaderText(ArchiveHeaderInfo info)
    {
        Console.WriteLine($"{"Signature",-30} {info.Signature}");
        Console.WriteLine($"{"Version",-30} {info.Version}");
        Console.WriteLine($"{"Unity Version",-30} {info.UnityVersion}");
        Console.WriteLine($"{"File Size",-30} {info.Size:N0} bytes");
        Console.WriteLine($"{"Compressed Metadata Size",-30} {info.CompressedMetadataSize:N0}");
        Console.WriteLine($"{"Uncompressed Metadata Size",-30} {info.UncompressedMetadataSize:N0}");
        Console.WriteLine($"{"Metadata Compression",-30} {FormatCompressionType(info.MetadataCompressionType)}");
        Console.WriteLine($"{"Flags",-30} {FormatArchiveFlags(info.ArchiveFlagBits)}");
    }

    static void OutputHeaderJson(ArchiveHeaderInfo info)
    {
        var jsonObject = new
        {
            signature = info.Signature,
            version = info.Version,
            unityVersion = info.UnityVersion,
            fileSize = info.Size,
            compressedMetadataSize = info.CompressedMetadataSize,
            uncompressedMetadataSize = info.UncompressedMetadataSize,
            metadataCompression = FormatCompressionType(info.MetadataCompressionType),
            flags = GetArchiveFlagNames(info.ArchiveFlagBits),
        };

        var json = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    static string FormatCompressionType(int compressionType)
    {
        return compressionType switch
        {
            0 => "None",
            1 => "Lzma",
            2 => "Lz4",
            3 => "Lz4HC",
            _ => compressionType.ToString(),
        };
    }

    static readonly (uint bit, string name)[] KnownArchiveFlags =
    {
        (0x40,  "BlocksAndDirectoryInfoCombined"),
        (0x80,  "BlocksInfoAtTheEnd"),
        (0x100, "OldWebPluginCompatibility"),
        (0x200, "BlockInfoNeedPaddingAtStart"),
    };

    static string[] GetArchiveFlagNames(uint flagBits)
    {
        var names = new List<string>();
        uint remaining = flagBits;

        foreach (var (bit, name) in KnownArchiveFlags)
        {
            if ((remaining & bit) != 0)
            {
                names.Add(name);
                remaining &= ~bit;
            }
        }

        // Report any unrecognized bits by hex value.
        if (remaining != 0)
            names.Add($"0x{remaining:X}");

        return names.ToArray();
    }

    static string FormatArchiveFlags(uint flagBits)
    {
        var names = GetArchiveFlagNames(flagBits);
        return names.Length > 0 ? string.Join(", ", names) : "None";
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
