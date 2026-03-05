using System;
using System.IO;
using System.Linq;
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
        // The object list is read directly from the parsed metadata rather than via UnityFileSystemApi.
        //
        // Advantages: works for any modern SerializedFile (version >= 19), including Player builds
        // that were compiled without TypeTrees — files that UnityFileSystemApi cannot open at all.
        //
        // Trade-offs: type names come from TypeIdRegistry rather than the file's embedded TypeTree,
        // so uncommon types not covered by the registry are displayed as a numeric TypeId. Files
        // older than version 19 (Unity 2019.1) are not supported by the metadata parser.
        //
        // These trade-offs are minor compared to the benefit of handling the common no-TypeTree case,
        // so there is no need to keep the UnityFileSystemApi code path.
        if (!ValidateSerializedFile(filename.FullName, out var fileInfo))
            return 1;

        if (!SerializedFileDetector.TryParseMetadata(filename.FullName, fileInfo, out var metadata, out var errorMessage))
        {
            Console.Error.WriteLine($"Error: Failed to parse object list for: {filename.FullName}");
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        if (metadata.ObjectList == null)
        {
            Console.Error.WriteLine($"Error: Object list could not be parsed for: {filename.FullName}");
            return 1;
        }

        if (format == OutputFormat.Json)
            OutputObjectListJson(metadata.ObjectList);
        else
            OutputObjectListText(metadata.ObjectList);

        return 0;
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

    public static int HandleMetadata(FileInfo filename, OutputFormat format)
    {
        if (!ValidateSerializedFile(filename.FullName, out var fileInfo))
            return 1;

        if (!SerializedFileDetector.TryParseMetadata(filename.FullName, fileInfo, out var metadata, out var errorMessage))
        {
            Console.Error.WriteLine($"Error: Failed to parse metadata for: {filename.FullName}");
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        if (format == OutputFormat.Json)
            OutputMetadataJson(metadata);
        else
            OutputMetadataText(metadata);

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

    private static void OutputObjectListText(ObjectInfo[] objects)
    {
        Console.WriteLine($"{"Id",-20} {"Type",-40} {"Offset",-15} {"Size",-15}");
        Console.WriteLine(new string('-', 90));

        foreach (var obj in objects)
            Console.WriteLine($"{obj.Id,-20} {TypeIdRegistry.GetTypeName(obj.TypeId),-40} {obj.Offset,-15} {obj.Size,-15}");
    }

    private static void OutputObjectListJson(ObjectInfo[] objects)
    {
        var jsonArray = new object[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            jsonArray[i] = new
            {
                id = obj.Id,
                typeId = obj.TypeId,
                typeName = TypeIdRegistry.GetTypeName(obj.TypeId),
                offset = obj.Offset,
                size = obj.Size
            };
        }

        var json = JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
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

    private static void OutputMetadataText(SerializedFileMetadata metadata)
    {
        string typeTreeDefinitions;
        if (!metadata.EnableTypeTree)
            typeTreeDefinitions = "No";
        else if (metadata.TypeTrees == null || metadata.TypeTrees.Length == 0)
            typeTreeDefinitions = "Unknown";
        else if (metadata.TypeTrees.All(t => t.InlineTypeTree))
            typeTreeDefinitions = "Inline";
        else if (metadata.TypeTrees.Any(t => t.InlineTypeTree))
            typeTreeDefinitions = "Mixed";  // unexpected: entries disagree on inline vs external
        else
            typeTreeDefinitions = "External";

        Console.WriteLine($"{"Unity Version",-20} {metadata.UnityVersion}");
        Console.WriteLine($"{"Target Platform",-20} {metadata.TargetPlatform}");
        Console.WriteLine($"{"TypeTree Definitions",-20} {typeTreeDefinitions}");
        Console.WriteLine($"{"TypeTree Count",-20} {metadata.TypeTreeCount}");
        Console.WriteLine($"{"RefType Count",-20} {metadata.SerializedReferenceTypeTreeCount}");
    }

    private static void OutputMetadataJson(SerializedFileMetadata metadata)
    {
        var jsonObject = new
        {
            unityVersion = metadata.UnityVersion,
            targetPlatform = metadata.TargetPlatform,
            enableTypeTree = metadata.EnableTypeTree,
            typeTreeCount = metadata.TypeTreeCount,
            serializedReferenceTypeTreeCount = metadata.SerializedReferenceTypeTreeCount,
            typeTrees = metadata.TypeTrees?.Select(TypeTreeInfoToJson).ToArray(),
            serializedReferenceTypeTrees = metadata.SerializedReferenceTypeTrees?.Select(TypeTreeInfoToJson).ToArray(),
        };

        var json = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    private static object TypeTreeInfoToJson(TypeTreeInfo info)
    {
        return new
        {
            persistentTypeID = info.PersistentTypeID,
            isStrippedType = info.IsStrippedType,
            scriptTypeIndex = info.ScriptTypeIndex,
            scriptID = info.ScriptID.ToString(),
            oldTypeHash = info.OldTypeHash.ToString(),
            typeTreeContentHash = info.TypeTreeContentHash.ToString(),
            typeTreeSerializedSize = info.TypeTreeSerializedSize,
            inlineTypeTree = info.InlineTypeTree,
            className = info.ClassName,
            namespaceName = info.Namespace,
            assemblyName = info.AssemblyName,
            typeDependencies = info.TypeDependencies,
        };
    }
}
