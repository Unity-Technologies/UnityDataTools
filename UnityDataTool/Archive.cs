using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool;

public static class Archive
{
    public static int HandleExtract(FileInfo filename, DirectoryInfo outputFolder, string filter = null)
    {
        try
        {
            var path = filename.ToString();
            if (WebBundleHelper.IsWebBundle(path))
            {
                WebBundleHelper.Extract(filename, outputFolder, filter);
            }
            else if (ArchiveDetector.IsUnityArchive(path))
            {
                ExtractAssetBundle(filename, outputFolder, filter);
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

    public static int HandleBlocks(FileInfo filename, OutputFormat format)
    {
        var path = filename.ToString();

        if (WebBundleHelper.IsWebBundle(path))
        {
            Console.Error.WriteLine("Web bundle files (.data, .data.gz, .data.br) use a different format. The blocks command is only supported for Unity Archive files.");
            return 1;
        }

        if (!ArchiveDetector.TryReadArchiveHeader(filename.FullName, out var header, out var errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        if (!ArchiveDetector.TryReadArchiveMetadata(filename.FullName, header, out var metadata, out errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        if (format == OutputFormat.Json)
            OutputBlocksJson(metadata.BlocksInfo);
        else
            OutputBlocksText(metadata.BlocksInfo);

        return 0;
    }

    public static int HandleInfo(FileInfo filename, OutputFormat format)
    {
        var path = filename.ToString();

        if (WebBundleHelper.IsWebBundle(path))
        {
            Console.Error.WriteLine("Web bundle files (.data, .data.gz, .data.br) use a different format. The info command is only supported for Unity Archive files.");
            return 1;
        }

        if (!ArchiveDetector.TryReadArchiveHeader(filename.FullName, out var header, out var errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        if (!ArchiveDetector.TryReadArchiveMetadata(filename.FullName, header, out var metadata, out errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        var blocks = metadata.BlocksInfo.Blocks;
        var nodes = metadata.DirectoryInfo.Nodes;

        long dataSize = 0;
        long uncompressedDataSize = 0;
        foreach (var block in blocks)
        {
            dataSize += block.CompressedSize;
            uncompressedDataSize += block.UncompressedSize;
        }

        // Determine the compression algorithm by finding the first block that uses compression.
        // Individual blocks may be stored uncompressed even when compression is enabled, because
        // compression is skipped when it provides no size reduction. So the first compressed block
        // tells us what algorithm was used for the archive.
        string compression = "Uncompressed";
        foreach (var block in blocks)
        {
            if (block.CompressionType != 0)
            {
                compression = FormatCompressionType(block.CompressionType);
                break;
            }
        }

        double compressionRatio = dataSize > 0 ? (double)uncompressedDataSize / dataSize : 0;
        int fileCount = nodes.Length;
        int serializedFileCount = 0;
        foreach (var node in nodes)
        {
            if ((node.Flags & 0x04) != 0)
                serializedFileCount++;
        }

        if (format == OutputFormat.Json)
        {
            var jsonObject = new
            {
                unityVersion = header.UnityVersion,
                fileSize = header.Size,
                dataSize = dataSize,
                uncompressedDataSize = uncompressedDataSize,
                compressionRatio = Math.Round(compressionRatio, 2),
                compression = compression,
                blockCount = blocks.Length,
                fileCount = fileCount,
                serializedFileCount = serializedFileCount,
            };
            var json = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"{"Unity Version",-30} {header.UnityVersion}");
            Console.WriteLine($"{"File Size",-30} {header.Size:N0} bytes");
            Console.WriteLine($"{"Data Size",-30} {dataSize:N0} bytes");
            Console.WriteLine($"{"Uncompressed Data Size",-30} {uncompressedDataSize:N0} bytes");
            Console.WriteLine($"{"Compression Ratio",-30} {compressionRatio:F2}x");
            Console.WriteLine($"{"Compression",-30} {compression}");
            Console.WriteLine($"{"Block Count",-30} {blocks.Length}");
            Console.WriteLine($"{"File Count",-30} {fileCount}");
            Console.WriteLine($"{"Serialized File Count",-30} {serializedFileCount}");
        }

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

    static void OutputBlocksText(ArchiveBlocksInfo blocksInfo)
    {
        Console.WriteLine($"Blocks: {blocksInfo.Blocks.Length}");
        for (int i = 0; i < blocksInfo.Blocks.Length; i++)
        {
            var block = blocksInfo.Blocks[i];
            Console.WriteLine($"  #{i,-4} FileOffset: {block.FileOffset:N0}  DataOffset: {block.DataOffset:N0}  Uncompressed: {block.UncompressedSize:N0}  Compressed: {block.CompressedSize:N0}  Compression: {FormatCompressionType(block.CompressionType)}");
        }
    }

    static void OutputBlocksJson(ArchiveBlocksInfo blocksInfo)
    {
        var jsonBlocks = new object[blocksInfo.Blocks.Length];
        for (int i = 0; i < blocksInfo.Blocks.Length; i++)
        {
            var block = blocksInfo.Blocks[i];
            jsonBlocks[i] = new
            {
                index = i,
                fileOffset = block.FileOffset,
                dataOffset = block.DataOffset,
                uncompressedSize = block.UncompressedSize,
                compressedSize = block.CompressedSize,
                compression = FormatCompressionType(block.CompressionType),
                isStreamed = block.IsStreamed,
            };
        }

        var jsonObject = new { blocks = jsonBlocks };
        var json = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    static readonly (uint bit, string name)[] KnownNodeFlags =
    {
        (0x01, "Directory"),
        (0x02, "Deleted"),
        (0x04, "SerializedFile"),
    };

    static string FormatNodeFlags(uint flags)
    {
        var names = new List<string>();
        uint remaining = flags;

        foreach (var (bit, name) in KnownNodeFlags)
        {
            if ((remaining & bit) != 0)
            {
                names.Add(name);
                remaining &= ~bit;
            }
        }

        if (remaining != 0)
            names.Add($"0x{remaining:X}");

        return names.Count > 0 ? string.Join(", ", names) : "None";
    }

    static void ExtractAssetBundle(FileInfo filename, DirectoryInfo outputFolder, string filter)
    {
        Console.WriteLine($"Extracting files from archive: {filename}");
        using var archive = UnityFileSystem.MountArchive(filename.FullName, "/");

        int total = archive.Nodes.Count;
        int extracted = 0;

        foreach (var node in archive.Nodes)
        {
            if (filter != null && !node.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            Console.WriteLine($"... Extracting {node.Path}");
            CopyFile("/" + node.Path, Path.Combine(outputFolder.FullName, node.Path));
            extracted++;
        }

        Console.WriteLine($"Extracted {extracted} out of {total} files.");
    }

    static void ListAssetBundle(FileInfo filename, OutputFormat format)
    {
        if (!ArchiveDetector.TryReadArchiveHeader(filename.FullName, out var header, out var errorMessage))
            throw new NotSupportedException(errorMessage);

        if (!ArchiveDetector.TryReadArchiveMetadata(filename.FullName, header, out var metadata, out errorMessage))
            throw new NotSupportedException(errorMessage);

        var nodes = metadata.DirectoryInfo.Nodes;

        if (format == OutputFormat.Json)
        {
            var jsonArray = new object[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                jsonArray[i] = new { path = node.Path, dataOffset = node.DataOffset, size = node.Size, flags = FormatNodeFlags(node.Flags) };
            }
            var json = JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            foreach (var node in nodes)
            {
                Console.WriteLine($"{node.Path}");
                Console.WriteLine($"  Data Offset: {node.DataOffset}");
                Console.WriteLine($"  Size: {node.Size}");
                Console.WriteLine($"  Flags: {FormatNodeFlags(node.Flags)}");
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
