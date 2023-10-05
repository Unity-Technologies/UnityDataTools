using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool;

public static class Archive
{
    private static readonly byte[] WebBundlePrefix = Encoding.UTF8.GetBytes("UnityWebData1.0\0");

    public static int HandleExtract(FileInfo filename, DirectoryInfo outputFolder)
    {
        try
        {
            if (IsWebBundle(filename))
            {
                ExtractWebBundle(filename, outputFolder);
            }
            else
            {
                ExtractAssetBundle(filename, outputFolder);
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

    public static int HandleList(FileInfo filename)
    {
        try
        {
            if (IsWebBundle(filename))
            {
                ListWebBundle(filename);
            }
            else
            {
                ListAssetBundle(filename);
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


    public static bool IsWebBundle(FileInfo filename)
    {
        var path = filename.ToString();
        return (
            path.EndsWith(".data")
            || path.EndsWith(".data.gz")
            || path.EndsWith(".data.br")
        );
    }

    struct WebBundleFileDescription
    {
        public uint ByteOffset;
        public uint Size;
        public string Path;
    }

    static void ExtractWebBundle(FileInfo filename, DirectoryInfo outputFolder) {
        Console.WriteLine($"Extracting web bundle: {filename}");
        using var fileStream = File.Open(filename.ToString(), FileMode.Open);
        using var stream = GetStream(filename, fileStream);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var fileDescriptions = ParseWebBundleHeader(reader);
        foreach (var description in fileDescriptions)
        {
            ExtractFileFromWebBundle(description, reader, outputFolder);
        }
    }

    static Stream GetStream(FileInfo filename, FileStream fileStream) {
        var fileExtension = Path.GetExtension(filename.ToString());
        return fileExtension switch
        {
            ".data" => fileStream,
            ".gz" => new GZipStream(fileStream, CompressionMode.Decompress),
            ".br" => new BrotliStream(fileStream, CompressionMode.Decompress),
            _ => throw new FileFormatException("Incorrect file extension for web bundle"),
        };
    }

    static List<WebBundleFileDescription> ParseWebBundleHeader(BinaryReader reader)
    {
        var result = new List<WebBundleFileDescription>();
        var prefix = ReadBytes(reader, WebBundlePrefix.Length);
        if (!prefix.SequenceEqual(WebBundlePrefix)) {
            throw new FileFormatException("File is not a valid web bundle.");
        }
        uint headerSize = ReadUInt32(reader);
        // Advance offset past prefix string and header size uint.
        var currentByteOffset = WebBundlePrefix.Length + sizeof(uint);
        while (currentByteOffset < headerSize)
        {
            var fileByteOffset = ReadUInt32(reader);
            var fileSize = ReadUInt32(reader);
            var filePathLength = ReadUInt32(reader);
            var filePath = Encoding.UTF8.GetString(ReadBytes(reader, (int) filePathLength));
            result.Add(new WebBundleFileDescription() {
                ByteOffset = fileByteOffset,
                Size = fileSize,
                Path = filePath,
            });
            // Advance byte offset, so we keep track of the position (to know when we're done reading the header).
            currentByteOffset += 3 * sizeof(uint) + filePath.Length;
        }
        return result;
    }

    static void ExtractFileFromWebBundle(WebBundleFileDescription description, BinaryReader reader, DirectoryInfo outputFolder)
    {
        // This function assumes `reader` is at the start of the binary data representing the file contents.
        Console.WriteLine($"... Extracting {description.Path}");
        var path = Path.Combine(outputFolder.ToString(), description.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, ReadBytes(reader, (int) description.Size));
    }

    static uint ReadUInt32(BinaryReader reader)
    {
        try {
            return reader.ReadUInt32();
        }
        catch (EndOfStreamException)
        {
            throw new FileFormatException("File data is corrupt.");
        }
    }

    static byte[] ReadBytes(BinaryReader reader, int count)
    {
        var result = reader.ReadBytes(count);
        if (result.Length != count)
        {
            throw new FileFormatException("File data is corrupt.");
        }
        return result;
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

    static void ListAssetBundle(FileInfo filename)
    {
        using var archive = UnityFileSystem.MountArchive(filename.FullName, "/");
        foreach (var node in archive.Nodes)
        {
            Console.WriteLine($"{node.Path}");
            Console.WriteLine($"  Size: {node.Size}");
            Console.WriteLine($"  Flags: {node.Flags}");
            Console.WriteLine();
        }
    }

    static void ListWebBundle(FileInfo filename)
    {
        using var fileStream = File.Open(filename.ToString(), FileMode.Open);
        using var stream = GetStream(filename, fileStream);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var fileDescriptions = ParseWebBundleHeader(reader);
        foreach (var description in fileDescriptions)
        {
            Console.WriteLine($"{description.Path}");
            Console.WriteLine($"  Size: {description.Size}");
            Console.WriteLine();
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
