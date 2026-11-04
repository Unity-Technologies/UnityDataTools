using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Archive;

public static class ArchiveTool
{
    public enum OutputFormat
    {
        Text,
        Json
    }

    public static int ExtractContent(FileInfo filename, DirectoryInfo outputFolder, string filter = null)
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
                ExtractArchive(filename, outputFolder, filter);
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

    public static int ListContent(FileInfo filename, OutputFormat format)
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
                ListArchive(filename, format);
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

    public static int PrintHeader(FileInfo filename, OutputFormat format)
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

    public static int ListBlocks(FileInfo filename, OutputFormat format)
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

    public static int PrintSummary(FileInfo filename, OutputFormat format)
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

        // The data section can be larger than the sum of the blocks, because the writer may leave
        // padding between them. Only version 9 and later record where each block starts, so this is
        // the only version where padding is detectable (and where it is currently written).
        long paddingSize = 0;
        if (blocks.Length > 0)
        {
            var lastBlock = blocks[blocks.Length - 1];
            paddingSize = lastBlock.Offset + lastBlock.CompressedSize - dataSize;
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
                blockPaddingSize = paddingSize,
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
            if (paddingSize > 0)
                Console.WriteLine($"{"Block Padding Size",-30} {paddingSize:N0} bytes");
            Console.WriteLine($"{"Uncompressed Data Size",-30} {uncompressedDataSize:N0} bytes");
            Console.WriteLine($"{"Compression Ratio",-30} {compressionRatio:F2}x");
            Console.WriteLine($"{"Compression",-30} {compression}");
            Console.WriteLine($"{"Block Count",-30} {blocks.Length}");
            Console.WriteLine($"{"File Count",-30} {fileCount}");
            Console.WriteLine($"{"Serialized File Count",-30} {serializedFileCount}");
        }

        return 0;
    }

    public static int Repack(FileInfo filename, FileInfo output, RepackOptions options, bool skipVerify, OutputFormat format)
    {
        var path = filename.ToString();

        if (WebBundleHelper.IsWebBundle(path))
        {
            Console.Error.WriteLine("Web bundle files (.data, .data.gz, .data.br) use a different format. The repack command is only supported for Unity Archive files.");
            return 1;
        }

        var optionsError = options.Validate();
        if (optionsError != null)
        {
            Console.Error.WriteLine(optionsError);
            return 1;
        }

        if (options.MaxCompressedBlockSize.HasValue && options.Compression == RepackCompression.None)
        {
            Console.Error.WriteLine("--max-compressed-block-size has no effect without compression.");
            return 1;
        }

        RepackResult result;
        try
        {
            result = ArchiveRepacker.Repack(filename.FullName, output.FullName, options, verify: !skipVerify);
        }
        catch (Exception err) when (
            err is ArgumentException
            || err is NotSupportedException
            || err is InvalidDataException
            || err is IOException
            || err is UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error repacking archive: {err.Message}");
            return 1;
        }

        if (format == OutputFormat.Json)
            OutputRepackJson(output, result);
        else
            OutputRepackText(filename, output, result);

        return 0;
    }

    static void OutputRepackText(FileInfo input, FileInfo output, RepackResult result)
    {
        var a = result.Original;
        var b = result.Repacked;
        double change = a.FileSize > 0 ? 100.0 * (b.FileSize - a.FileSize) / a.FileSize : 0;

        Console.WriteLine($"Repacked {input.Name} into {output.FullName}");
        Console.WriteLine();
        Console.WriteLine($"{"",-30} {"Original",15} {"Repacked",15}");
        Console.WriteLine($"{"File Size",-30} {a.FileSize,15:N0} {b.FileSize,15:N0}  ({change:+0.0;-0.0;0.0}%)");
        Console.WriteLine($"{"Compression",-30} {a.Compression,15} {b.Compression,15}");
        Console.WriteLine($"{"Block Count",-30} {a.BlockCount,15:N0} {b.BlockCount,15:N0}");
        Console.WriteLine($"{"Uncompressed Blocks",-30} {a.UncompressedBlockCount,15:N0} {b.UncompressedBlockCount,15:N0}");
        Console.WriteLine($"{"Largest Block",-30} {a.LargestBlockSize,15:N0} {b.LargestBlockSize,15:N0}");
        Console.WriteLine($"{"Read Amplification",-30} {a.ReadAmplification,14:F2}x {b.ReadAmplification,14:F2}x");
        Console.WriteLine();
        if (result.Verified)
            Console.WriteLine($"Verified: all {result.FileCount} files match the original byte for byte.");
        else
            Console.WriteLine("Not verified (--skip-verify).");
    }

    static void OutputRepackJson(FileInfo output, RepackResult result)
    {
        var jsonObject = new
        {
            output = output.FullName,
            fileCount = result.FileCount,
            verified = result.Verified,
            original = StatsToJson(result.Original),
            repacked = StatsToJson(result.Repacked),
        };
        var json = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    static object StatsToJson(ArchiveLayoutStats stats)
    {
        return new
        {
            fileSize = stats.FileSize,
            compression = stats.Compression,
            blockCount = stats.BlockCount,
            uncompressedBlockCount = stats.UncompressedBlockCount,
            largestBlockSize = stats.LargestBlockSize,
            dataSize = stats.DataSize,
            uncompressedDataSize = stats.UncompressedDataSize,
            readAmplification = Math.Round(stats.ReadAmplification, 3),
        };
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
        (ArchiveFlags.BlocksAndDirectoryInfoCombined, "BlocksAndDirectoryInfoCombined"),
        (ArchiveFlags.BlocksInfoAtTheEnd,             "BlocksInfoAtTheEnd"),
        (ArchiveFlags.OldWebPluginCompatibility,      "OldWebPluginCompatibility"),
        (ArchiveFlags.BlockInfoNeedPaddingAtStart,    "BlockInfoNeedPaddingAtStart"),
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
        (0x01, "Directory"), // In practice this is not used
        (0x02, "Deleted"),   // In practice this is not used
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

    static void ExtractArchive(FileInfo filename, DirectoryInfo outputFolder, string filter)
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

    static void ListArchive(FileInfo filename, OutputFormat format)
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
