using System;
using System.IO;
using System.Text.Json;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool;

public static class SerializedFileCommands
{
    public static int HandleExternalRefs(FileInfo filename, OutputFormat format)
    {
        try
        {
            using var sf = UnityFileSystem.OpenSerializedFile(filename.FullName);

            if (format == OutputFormat.Json)
                OutputExternalRefsJson(sf);
            else
                OutputExternalRefsText(sf);
        }
        catch (Exception err) when (err is NotSupportedException || err is FileFormatException)
        {
            Console.Error.WriteLine($"Error opening serialized file: {filename.FullName}");
            Console.Error.WriteLine(err.Message);
            return 1;
        }

        return 0;
    }

    public static int HandleObjectList(FileInfo filename, OutputFormat format)
    {
        try
        {
            using var sf = UnityFileSystem.OpenSerializedFile(filename.FullName);

            if (format == OutputFormat.Json)
                OutputObjectListJson(sf);
            else
                OutputObjectListText(sf);
        }
        catch (Exception err) when (err is NotSupportedException || err is FileFormatException)
        {
            Console.Error.WriteLine($"Error opening serialized file: {filename.FullName}");
            Console.Error.WriteLine(err.Message);
            return 1;
        }

        return 0;
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
}

