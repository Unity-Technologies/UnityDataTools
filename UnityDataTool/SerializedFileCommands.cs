using System;
using System.IO;
using System.Text.Json;
using UnityDataTools.Analyzer.Util;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool;

public static class SerializedFileCommands
{
    public static int HandleExternalRefs(FileInfo filename, OutputFormat format)
    {
        if (!ValidateSerializedFile(filename.FullName, out _))
            return 1;

        try
        {
            using var sf = UnityFileSystem.OpenSerializedFile(filename.FullName);
            if (format == OutputFormat.Json)
                OutputExternalRefsJson(sf);
            else
                OutputExternalRefsText(sf);
            return 0;
        }
        catch (Exception err) when (err is NotSupportedException || err is FileFormatException)
        {
            Console.Error.WriteLine($"Error opening SerializedFile: {filename.FullName}");
            Console.Error.WriteLine(err.Message);
            return 1;
        }
    }

    public static int HandleObjectList(FileInfo filename, OutputFormat format)
    {
        if (!ValidateSerializedFile(filename.FullName, out _))
            return 1;

        try
        {
            using var sf = UnityFileSystem.OpenSerializedFile(filename.FullName);
            if (format == OutputFormat.Json)
                OutputObjectListJson(sf);
            else
                OutputObjectListText(sf);
            return 0;
        }
        catch (Exception err) when (err is NotSupportedException || err is FileFormatException)
        {
            Console.Error.WriteLine($"Error opening SerializedFile: {filename.FullName}");
            Console.Error.WriteLine(err.Message);
            return 1;
        }
    }

    public static int HandleHeader(FileInfo filename, OutputFormat format)
    {
        if (!ValidateSerializedFile(filename.FullName, out var fileInfo))
            return 1;

        if (format == OutputFormat.Json)
            OutputHeaderJson(fileInfo);
        else
            OutputHeaderText(fileInfo);

        return 0;
    }

    /// <summary>
    /// Validates that a file is a SerializedFile and provides helpful error messages if not.
    /// </summary>
    /// <param name="filePath">Path to the file to validate</param>
    /// <param name="fileInfo">SerializedFile header information if valid, null otherwise</param>
    /// <returns>True if valid SerializedFile, false otherwise</returns>
    private static bool ValidateSerializedFile(string filePath, out SerializedFileInfo fileInfo)
    {
        fileInfo = null;

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return false;
        }

        if (ArchiveDetector.IsUnityArchive(filePath))
        {
            Console.Error.WriteLine($"Error: The file is an AssetBundle or other Unity Archive, not a SerializedFile.");
            Console.Error.WriteLine($"File: {filePath}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Unity Archives contain SerializedFiles inside them.");
            Console.Error.WriteLine("To access the SerializedFiles, first extract the archive using:");
            Console.Error.WriteLine($"  UnityDataTool archive extract \"{filePath}\" -o <output-directory>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Then you can run serialized-file commands on the extracted files.");
            return false;
        }

        if (YamlSerializedFileDetector.IsYamlSerializedFile(filePath))
        {
            Console.Error.WriteLine($"Error: The file is a YAML-format SerializedFile, which is not supported.");
            Console.Error.WriteLine($"File: {filePath}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("UnityDataTool only supports binary-format SerializedFiles.");
            return false;
        }

        if (!SerializedFileDetector.TryDetectSerializedFile(filePath, out fileInfo))
        {
            Console.Error.WriteLine($"Error: The file does not appear to be a valid Unity SerializedFile.");
            Console.Error.WriteLine($"File: {filePath}");
            return false;
        }

        return true;
    }

    private static void OutputExternalRefsText(SerializedFile sf)
    {
        var refs = sf.ExternalReferences;

        for (int i = 0; i < refs.Count; i++)
        {
            var extRef = refs[i];
            var displayValue = !string.IsNullOrEmpty(extRef.Path) ? extRef.Path : extRef.Guid;
            Console.WriteLine($"Index: {i + 1}, Path: {displayValue}");
        }
    }

    private static void OutputExternalRefsJson(SerializedFile sf)
    {
        var refs = sf.ExternalReferences;
        var jsonArray = new object[refs.Count];

        for (int i = 0; i < refs.Count; i++)
        {
            var extRef = refs[i];
            jsonArray[i] = new
            {
                index = i + 1,
                path = extRef.Path,
                guid = extRef.Guid,
                type = extRef.Type.ToString()
            };
        }

        var json = JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    private static void OutputObjectListText(SerializedFile sf)
    {
        var objects = sf.Objects;

        // Print header
        Console.WriteLine($"{"Id",-20} {"Type",-40} {"Offset",-15} {"Size",-15}");
        Console.WriteLine(new string('-', 90));

        foreach (var obj in objects)
        {
            string typeName = GetTypeName(sf, obj);
            Console.WriteLine($"{obj.Id,-20} {typeName,-40} {obj.Offset,-15} {obj.Size,-15}");
        }
    }

    private static void OutputObjectListJson(SerializedFile sf)
    {
        var objects = sf.Objects;
        var jsonArray = new object[objects.Count];

        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            string typeName = GetTypeName(sf, obj);

            jsonArray[i] = new
            {
                id = obj.Id,
                typeId = obj.TypeId,
                typeName = typeName,
                offset = obj.Offset,
                size = obj.Size
            };
        }

        var json = JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    private static string GetTypeName(SerializedFile sf, ObjectInfo obj)
    {
        try
        {
            // Try to get type name from TypeTree first (most accurate)
            var root = sf.GetTypeTreeRoot(obj.Id);
            return root.Type;
        }
        catch
        {
            // Fall back to registry if TypeTree is not available
            return TypeIdRegistry.GetTypeName(obj.TypeId);
        }
    }

    private static void OutputHeaderText(SerializedFileInfo info)
    {
        Console.WriteLine($"{"Version",-20} {info.Version}");
        Console.WriteLine($"{"Format",-20} {(info.IsLegacyFormat ? "Legacy (32-bit)" : "Modern (64-bit)")}");
        Console.WriteLine($"{"File Size",-20} {info.FileSize:N0} bytes");
        Console.WriteLine($"{"Metadata Size",-20} {info.MetadataSize:N0} bytes");
        Console.WriteLine($"{"Data Offset",-20} {info.DataOffset:N0}");
        Console.WriteLine($"{"Endianness",-20} {(info.Endianness == 0 ? "Little Endian" : "Big Endian")}");
    }

    private static void OutputHeaderJson(SerializedFileInfo info)
    {
        var jsonObject = new
        {
            version = info.Version,
            format = info.IsLegacyFormat ? "Legacy (32-bit)" : "Modern (64-bit)",
            fileSize = info.FileSize,
            metadataSize = info.MetadataSize,
            dataOffset = info.DataOffset,
            endianness = info.Endianness == 0 ? "Little Endian" : "Big Endian"
        };

        var json = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
}
