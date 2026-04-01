using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace UnityDataTools.UnityDataTool;

public static class WebBundleHelper
{
    private static readonly byte[] WebBundlePrefix = Encoding.UTF8.GetBytes("UnityWebData1.0\0");

    public static bool IsWebBundle(string path)
    {
        return (
            path.EndsWith(".data")
            || path.EndsWith(".data.gz")
            || path.EndsWith(".data.br")
        );
    }

    public static void Extract(FileInfo filename, DirectoryInfo outputFolder)
    {
        Console.WriteLine($"Extracting web bundle: {filename}");
        using var fileStream = File.Open(filename.ToString(), FileMode.Open);
        using var stream = GetStream(filename, fileStream);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var fileDescriptions = ParseWebBundleHeader(reader);
        foreach (var description in fileDescriptions)
        {
            ExtractFile(description, reader, outputFolder);
        }
    }

    public static void List(FileInfo filename, OutputFormat format)
    {
        using var fileStream = File.Open(filename.ToString(), FileMode.Open);
        using var stream = GetStream(filename, fileStream);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var fileDescriptions = ParseWebBundleHeader(reader);

        if (format == OutputFormat.Json)
        {
            var jsonArray = new object[fileDescriptions.Count];
            for (int i = 0; i < fileDescriptions.Count; i++)
            {
                var desc = fileDescriptions[i];
                jsonArray[i] = new { path = desc.Path, size = desc.Size };
            }
            var json = JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            foreach (var description in fileDescriptions)
            {
                Console.WriteLine($"{description.Path}");
                Console.WriteLine($"  Size: {description.Size}");
                Console.WriteLine();
            }
        }
    }

    struct FileDescription
    {
        public uint ByteOffset;
        public uint Size;
        public string Path;
    }

    static Stream GetStream(FileInfo filename, FileStream fileStream)
    {
        var fileExtension = Path.GetExtension(filename.ToString());
        return fileExtension switch
        {
            ".data" => fileStream,
            ".gz" => new GZipStream(fileStream, CompressionMode.Decompress),
            ".br" => new BrotliStream(fileStream, CompressionMode.Decompress),
            _ => throw new FileFormatException("Incorrect file extension for web bundle"),
        };
    }

    static List<FileDescription> ParseWebBundleHeader(BinaryReader reader)
    {
        var result = new List<FileDescription>();
        var prefix = ReadBytes(reader, WebBundlePrefix.Length);
        if (!prefix.SequenceEqual(WebBundlePrefix))
        {
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
            var filePath = Encoding.UTF8.GetString(ReadBytes(reader, (int)filePathLength));
            result.Add(new FileDescription()
            {
                ByteOffset = fileByteOffset,
                Size = fileSize,
                Path = filePath,
            });
            // Advance byte offset, so we keep track of the position (to know when we're done reading the header).
            currentByteOffset += 3 * sizeof(uint) + filePath.Length;
        }
        return result;
    }

    static void ExtractFile(FileDescription description, BinaryReader reader, DirectoryInfo outputFolder)
    {
        // This function assumes `reader` is at the start of the binary data representing the file contents.
        Console.WriteLine($"... Extracting {description.Path}");
        var path = Path.Combine(outputFolder.ToString(), description.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, ReadBytes(reader, (int)description.Size));
    }

    static uint ReadUInt32(BinaryReader reader)
    {
        try
        {
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
}
