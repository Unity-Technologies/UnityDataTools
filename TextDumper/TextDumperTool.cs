using System;
using System.IO;
using System.Text;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.TextDumper;

public class TextDumperTool
{
    // Arrays of basic types with more elements than this are summarized with a hash
    // unless --show-large-arrays is passed.
    const int MaxInlineArraySize = 256;

    StringBuilder m_StringBuilder = new StringBuilder(1024);
    DumpOptions m_Options;
    string m_TypeFilter;     // m_Options.TypeFilter normalized: null when blank/unset, otherwise the user-provided string
    int m_FilterTypeId;      // > 0 when filtering by Unity ClassID (numeric form of m_TypeFilter); 0 means no ID filter

    TextWriter m_Writer; // Output, either to a file or Console.Out

    // Set during the processed of each Serialized File
    UnityFileReader m_Reader;
    SerializedFile m_SerializedFile;

    public enum DumpFormat
    {
        Text,
    }

    public class DumpOptions
    {
        public DumpFormat Format { get; init; } = DumpFormat.Text;
        public string Path { get; init; }
        public string OutputPath { get; init; }
        public bool ShowLargeArrays { get; init; }
        public long ObjectId { get; init; }
        public string TypeFilter { get; init; }
        public bool ToStdout { get; init; }
    }

    public int Dump(DumpOptions options)
    {
        m_Options = options;
        m_TypeFilter = string.IsNullOrWhiteSpace(m_Options.TypeFilter) ? null : m_Options.TypeFilter;
        m_FilterTypeId = (m_TypeFilter != null && int.TryParse(m_TypeFilter, out var parsed) && parsed > 0) ? parsed : 0;

        try
        {
            if (!File.Exists(m_Options.Path))
            {
                Console.Error.WriteLine($"Error: File not found: {m_Options.Path}");
                return 1;
            }

            if (ArchiveDetector.IsUnityArchive(m_Options.Path))
                return DumpArchive();

            if (YamlSerializedFileDetector.IsYamlSerializedFile(m_Options.Path))
            {
                Console.Error.WriteLine("Error: The file is a YAML-format SerializedFile, which is not supported.");
                Console.Error.WriteLine("UnityDataTool only supports binary-format SerializedFiles.");
                return 1;
            }

            if (SerializedFileDetector.TryDetectSerializedFile(m_Options.Path, out _))
                return DumpSerializedFile();

            Console.Error.WriteLine("Error: The file does not appear to be a valid Unity SerializedFile or Unity Archive.");
            Console.Error.WriteLine($"File: {m_Options.Path}");
            return 1;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: {e.GetType()}: {e.Message}");
            Console.Error.WriteLine(e.StackTrace);
            return 1;
        }
    }

    int DumpSerializedFile()
    {
        if (ReportIfMissingTypeTrees(m_Options.Path, m_Options.Path))
            return 1;

        try
        {
            if (m_Options.ToStdout)
            {
                m_Writer = Console.Out;
                OutputSerializedFile(m_Options.Path);
                m_Writer.Flush();
            }
            else
            {
                using var writer = new StreamWriter(Path.Combine(m_Options.OutputPath, Path.GetFileName(m_Options.Path) + ".txt"), false);
                m_Writer = writer;
                OutputSerializedFile(m_Options.Path);
            }
        }
        catch (SerializedFileOpenException)
        {
            Console.Error.WriteLine($"Error: Failed to open serialized file: {m_Options.Path}");
            return 1;
        }

        return 0;
    }

    // dump needs TypeTrees to interpret object data, so a SerializedFile without them cannot be dumped.
    // Detecting this up front avoids handing the file to the native loader, which would otherwise emit
    // misleading version mismatch errors or crash the process. Returns true (and prints a clear message)
    // when the file has no TypeTrees. The path may be a real file or an entry in a mounted archive.
    bool ReportIfMissingTypeTrees(string path, string displayName)
    {
        using var stream = new UnityFileStream(path);
        if (!SerializedFileDetector.IsMissingTypeTrees(stream))
            return false;

        Console.Error.WriteLine($"Error: \"{displayName}\" has no TypeTrees. The dump command needs TypeTrees to interpret the serialized object data, so this file cannot be dumped.");
        return true;
    }

    // For convenience we also support directly dumping serialized files that are inside an archive,
    // so that it's not necessary to use `archive extract` if you only want to see values from the object serialization.
    int DumpArchive()
    {
        using var archive = UnityFileSystem.MountArchive(m_Options.Path, "/");
        bool anyMissingTypeTrees = false;

        if (m_Options.ToStdout)
        {
            ArchiveNode? singleSerializedFile = null;
            int serializedFileCount = 0;
            foreach (var node in archive.Nodes)
            {
                if (node.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                {
                    ++serializedFileCount;
                    singleSerializedFile ??= node;
                }
            }

            if (serializedFileCount == 0)
            {
                Console.Error.WriteLine("Error: Archive contains no SerializedFiles.");
                return 1;
            }

            if (serializedFileCount > 1)
            {
                Console.Error.WriteLine($"Error: --stdout cannot be used with an archive containing multiple SerializedFiles ({serializedFileCount} found).");
                Console.Error.WriteLine("Extract the archive first, or pass an individual SerializedFile as input.");
                return 1;
            }

            var node2 = singleSerializedFile.Value;
            Console.Error.WriteLine($"Processing {node2.Path} {node2.Size} {node2.Flags}");
            if (ReportIfMissingTypeTrees("/" + node2.Path, node2.Path))
                return 1;
            m_Writer = Console.Out;
            OutputSerializedFile("/" + node2.Path);
            m_Writer.Flush();
        }
        else
        {
            foreach (var node in archive.Nodes)
            {
                Console.WriteLine($"Processing {node.Path} {node.Size} {node.Flags}");

                if (node.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                {
                    if (ReportIfMissingTypeTrees("/" + node.Path, node.Path))
                    {
                        anyMissingTypeTrees = true;
                        continue;
                    }

                    using var writer = new StreamWriter(Path.Combine(m_Options.OutputPath, Path.GetFileName(node.Path) + ".txt"), false);
                    m_Writer = writer;
                    OutputSerializedFile("/" + node.Path);
                }
            }
        }

        return anyMissingTypeTrees ? 1 : 0;
    }

    void OutputSerializedFile(string path)
    {
        var objectId = m_Options.ObjectId;
        bool filtered = objectId != 0 || m_TypeFilter != null;

        using (m_Reader = new UnityFileReader(path, 64 * 1024 * 1024))
        using (m_SerializedFile = UnityFileSystem.OpenSerializedFile(path))
        {
            // External references provide context for PPtrs across the whole file. Skip them when a
            // filter is in use - the output is about a specific object, and `sf externalrefs` is the
            // dedicated command for listing external refs.
            if (!filtered)
            {
                var i = 1;

                m_Writer.WriteLine("External References");
                foreach (var extRef in m_SerializedFile.ExternalReferences)
                {
                    m_Writer.WriteLine($"path({i}): \"{extRef.Path}\" GUID: {extRef.Guid} Type: {(int)extRef.Type}");
                    ++i;
                }
                m_Writer.WriteLine();
            }

            bool dumpedObject = false;
            foreach (var obj in m_SerializedFile.Objects)
            {
                if (objectId != 0 && obj.Id != objectId)
                    continue;

                if (m_FilterTypeId > 0 && obj.TypeId != m_FilterTypeId)
                    continue;

                var root = m_SerializedFile.GetTypeTreeRoot(obj.Id);

                if (m_TypeFilter != null && m_FilterTypeId == 0 && !MatchesTypeNameFilter(obj, root))
                    continue;

                var offset = obj.Offset;

                m_Writer.Write($"ID: {obj.Id} (ClassID: {obj.TypeId}) ");
                RecursiveDump(root, ref offset, 0);
                m_Writer.WriteLine();
                dumpedObject = true;
            }

            if (filtered && !dumpedObject)
            {
                if (objectId != 0)
                    m_Writer.WriteLine($"Object with ID {objectId} not found.");
                else
                    m_Writer.WriteLine($"No objects found matching type \"{m_TypeFilter}\".");
            }
        }
    }

    void RecursiveDump(TypeTreeNode node, ref long offset, int level, int arrayIndex = -1)
    {
        bool skipChildren = false;

        if (level > 1 && node.IsManagedReferenceRegistry)
        {
            // If we are already inside a ManagedReferenceRegistry, then we ignore the ManagedReferenceRegistry node
            // they can appear in the TypeTrees of Managed objects, but only the root object actually has a registry
            skipChildren = true;
        }
        else if (node.IsArray)
        {
            DumpArray(node, ref offset, level);

            // Skip child nodes as they were already processed here.
            skipChildren = true;
        }
        else
        {
            AppendIndent(level);

            if (level != 0)
            {
                m_StringBuilder.Append(node.Name);
                if (arrayIndex >= 0)
                {
                    m_StringBuilder.Append('[');
                    m_StringBuilder.Append(arrayIndex);
                    m_StringBuilder.Append(']');
                }
                m_StringBuilder.Append(' ');
                m_StringBuilder.Append('(');
                m_StringBuilder.Append(node.Type);
                m_StringBuilder.Append(')');
            }
            else
            {
                m_StringBuilder.Append(node.Type);
            }

            if (node.IsBasicType)
            {
                m_StringBuilder.Append(' ');
                m_StringBuilder.Append(ReadValue(node, offset));

                offset += node.Size;
            }
            else if (node.Type == "string")
            {
                var stringSize = m_Reader.ReadInt32(offset);

                m_StringBuilder.Append(' ');
                m_StringBuilder.Append(m_Reader.ReadString(offset + 4, stringSize));

                offset += stringSize + 4;

                // Skip child nodes as they were already processed here.
                skipChildren = true;
            }
            else if (TryReadCompoundValue(node, ref offset, out var compoundValue))
            {
                m_StringBuilder.Append(' ');
                m_StringBuilder.Append(compoundValue);

                // Skip child nodes as they were already processed here.
                skipChildren = true;
            }

            m_Writer.WriteLine(m_StringBuilder);
            m_StringBuilder.Clear();

            if (node.IsManagedReferenceRegistry)
            {
                DumpManagedReferenceRegistry(node, ref offset, level + 1);

                // Skip child nodes as they were already processed here.
                skipChildren = true;
            }
        }

        if (!skipChildren)
        {
            foreach (var child in node.Children)
            {
                RecursiveDump(child, ref offset, level + 1);
            }
        }

        if (
            ((int)node.MetaFlags & (int)TypeTreeMetaFlags.AlignBytes) != 0 ||
            ((int)node.MetaFlags & (int)TypeTreeMetaFlags.AnyChildUsesAlignBytes) != 0
        )
        {
            offset = AlignTo4(offset);
        }
    }

    void DumpArray(TypeTreeNode node, ref long offset, int level)
    {
        // First child contains array size.
        var sizeNode = node.Children[0];
        // Second child contains array type information.
        var dataNode = node.Children[1];

        if (sizeNode.Size != 4 || !sizeNode.IsLeaf)
            throw new Exception("Invalid array size");

        var arraySize = m_Reader.ReadInt32(offset);
        offset += 4;

        AppendIndent(level);
        m_StringBuilder.Append("Array");
        m_StringBuilder.Append('<');
        m_StringBuilder.Append(dataNode.Type);
        m_StringBuilder.Append(">[");
        m_StringBuilder.Append(arraySize);
        m_StringBuilder.Append(']');

        m_Writer.WriteLine(m_StringBuilder);
        m_StringBuilder.Clear();

        if (arraySize > 0)
        {
            if (dataNode.IsBasicType)
            {
                AppendIndent(level + 1);

                if (arraySize > MaxInlineArraySize && !m_Options.ShowLargeArrays)
                {
                    // Summarizing with a hash keeps the output readable while a diff of two dumps
                    // still detects content changes (same idea as binary2text -largebinaryhashonly).
                    m_StringBuilder.Append("ArrayDataHash ");
                    m_StringBuilder.Append(m_Reader.ComputeCRC(offset, dataNode.Size * arraySize).ToString("x8"));
                    offset += dataNode.Size * arraySize;
                }
                else
                {
                    var array = ReadBasicTypeArray(dataNode, offset, arraySize);
                    offset += dataNode.Size * arraySize;

                    m_StringBuilder.Append(array.GetValue(0));
                    for (int i = 1; i < arraySize; ++i)
                    {
                        m_StringBuilder.Append(", ");
                        m_StringBuilder.Append(array.GetValue(i));
                    }
                }

                m_Writer.WriteLine(m_StringBuilder);
                m_StringBuilder.Clear();
            }
            else
            {
                ++level;

                for (int i = 0; i < arraySize; ++i)
                {
                    RecursiveDump(dataNode, ref offset, level, i);
                }
            }
        }
    }

    void DumpManagedReferenceRegistry(TypeTreeNode node, ref long offset, int level)
    {
        if (node.Children.Count < 2)
            throw new Exception("Invalid ManagedReferenceRegistry");

        // First child is version number.
        var version = m_Reader.ReadInt32(offset);
        RecursiveDump(node.Children[0], ref offset, level);

        TypeTreeNode refTypeNode;
        TypeTreeNode refObjData;

        if (version == 1)
        {
            // Second child is the ReferencedObject.
            var refObjNode = node.Children[1];
            // And its children are the referenced type and data nodes.
            refTypeNode = refObjNode.Children[0];
            refObjData = refObjNode.Children[1];

            int i = 0;

            while (DumpManagedReferenceData(refTypeNode, refObjData, ref offset, level, i++))
            { }
        }
        else if (version == 2)
        {
            // Second child is the RefIds vector.
            var refIdsVectorNode = node.Children[1];

            if (refIdsVectorNode.Children.Count < 1 || refIdsVectorNode.Name != "RefIds")
                throw new Exception("Invalid ManagedReferenceRegistry RefIds vector");

            var refIdsArrayNode = refIdsVectorNode.Children[0];

            if (refIdsArrayNode.Children.Count != 2 || !refIdsArrayNode.IsArray)
                throw new Exception("Invalid ManagedReferenceRegistry RefIds array");

            // First child is the array size.
            int arraySize = m_Reader.ReadInt32(offset);
            offset += 4;

            // Second child is the ReferencedObject.
            var refObjNode = refIdsArrayNode.Children[1];

            for (int i = 0; i < arraySize; ++i)
            {
                // First child is the rid.
                long rid = m_Reader.ReadInt64(offset);
                offset += 8;

                // And the next children are the referenced type and data nodes.
                refTypeNode = refObjNode.Children[1];
                refObjData = refObjNode.Children[2];
                DumpManagedReferenceData(refTypeNode, refObjData, ref offset, level, rid);
            }
        }
        else
        {
            throw new Exception($"Unsupported ManagedReferenceRegistry version {version}");
        }
    }

    bool DumpManagedReferenceData(TypeTreeNode refTypeNode, TypeTreeNode referencedTypeDataNode, ref long offset, int level, long id)
    {
        if (refTypeNode.Children.Count < 3)
            throw new Exception("Invalid ReferencedManagedType");

        AppendIndent(level);
        m_StringBuilder.Append("rid(");
        m_StringBuilder.Append(id);
        m_StringBuilder.Append(") ReferencedObject");

        m_Writer.WriteLine(m_StringBuilder);
        m_StringBuilder.Clear();

        ++level;

        var refTypeOffset = offset;
        var className = ReadPascalStringAndAlign(ref offset);
        var namespaceName = ReadPascalStringAndAlign(ref offset);
        var assemblyName = ReadPascalStringAndAlign(ref offset);

        if (IsTerminusSentinel(className, namespaceName, assemblyName))
            return false;

        // Not the most efficient way, but it simplifies the code.
        RecursiveDump(refTypeNode, ref refTypeOffset, level);

        AppendIndent(level);
        m_StringBuilder.Append(referencedTypeDataNode.Name);
        m_StringBuilder.Append(' ');
        m_StringBuilder.Append(referencedTypeDataNode.Type);
        m_StringBuilder.Append(' ');

        m_Writer.WriteLine(m_StringBuilder);
        m_StringBuilder.Clear();

        if (id == -1 || id == -2)
        {
            AppendIndent(level);
            m_StringBuilder.Append(id == -1 ? "  unknown" : "  null");

            m_Writer.WriteLine(m_StringBuilder);
            m_StringBuilder.Clear();

            return true;
        }

        var refTypeRoot = m_SerializedFile.GetRefTypeTypeTreeRoot(className, namespaceName, assemblyName);

        // Dump the ReferencedObject using its own TypeTree, but skip the root.
        foreach (var child in refTypeRoot.Children)
        {
            RecursiveDump(child, ref offset, level + 1);
        }

        return true;
    }

    // Compound types that are more readable printed as a single value than as their serialized
    // fields, similarly to strings. To support another type, add a case matching its type name and
    // serialized layout, returning the formatted value and advancing offset past the data.
    bool TryReadCompoundValue(TypeTreeNode node, ref long offset, out string value)
    {
        switch (node.Type)
        {
            // A GUID is serialized as 4 uint32 values.
            case "GUID" when HasUniformLeafChildren(node, 4, 4):
                value = GuidHelper.FormatUnityGuid(
                    m_Reader.ReadUInt32(offset),
                    m_Reader.ReadUInt32(offset + 4),
                    m_Reader.ReadUInt32(offset + 8),
                    m_Reader.ReadUInt32(offset + 12));
                offset += 16;
                return true;

            // A Hash128 is serialized as 16 bytes.
            case "Hash128" when HasUniformLeafChildren(node, 16, 1):
                var bytes = new byte[16];
                m_Reader.ReadArray(offset, 16, bytes);
                value = GuidHelper.FormatUnityHash128(bytes);
                offset += 16;
                return true;
        }

        value = null;
        return false;
    }

    // Confirms a compound type has the exact layout expected by TryReadCompoundValue, so that an
    // unrelated type reusing the same name falls back to the generic field-by-field dump.
    static bool HasUniformLeafChildren(TypeTreeNode node, int count, int size)
    {
        if (node.Children.Count != count)
            return false;

        foreach (var child in node.Children)
        {
            if (!child.IsLeaf || child.Size != size)
                return false;
        }

        return true;
    }

    static long AlignTo4(long offset) => (offset + 3) & ~3L;

    void AppendIndent(int level) => m_StringBuilder.Append(' ', level * 2);

    string ReadPascalStringAndAlign(ref long offset)
    {
        var size = m_Reader.ReadInt32(offset);
        var value = m_Reader.ReadString(offset + 4, size);
        offset = AlignTo4(offset + 4 + size);
        return value;
    }

    // Sentinel record that marks the end of the v1 ReferencedObject sequence.
    static bool IsTerminusSentinel(string className, string namespaceName, string assemblyName) =>
        className == "Terminus" && namespaceName == "UnityEngine.DMAT" && assemblyName == "FAKE_ASM";

    bool MatchesTypeNameFilter(ObjectInfo obj, TypeTreeNode root)
    {
        var typeName = TypeIdRegistry.GetTypeName(obj.TypeId);
        // GetTypeName returns the id as a string when the type is unknown;
        // fall back to the TypeTree root node for script types.
        if (typeName == obj.TypeId.ToString())
            typeName = root.Type;
        return string.Equals(typeName, m_TypeFilter, StringComparison.OrdinalIgnoreCase);
    }

    string ReadValue(TypeTreeNode node, long offset)
    {
        switch (Type.GetTypeCode(node.CSharpType))
        {
            case TypeCode.Int32:
                return m_Reader.ReadInt32(offset).ToString();

            case TypeCode.UInt32:
                return m_Reader.ReadUInt32(offset).ToString();

            case TypeCode.Single:
                return m_Reader.ReadFloat(offset).ToString();

            case TypeCode.Double:
                return m_Reader.ReadDouble(offset).ToString();

            case TypeCode.Int16:
                return m_Reader.ReadInt16(offset).ToString();

            case TypeCode.UInt16:
                return m_Reader.ReadUInt16(offset).ToString();

            case TypeCode.Int64:
                return m_Reader.ReadInt64(offset).ToString();

            case TypeCode.UInt64:
                return m_Reader.ReadUInt64(offset).ToString();

            case TypeCode.SByte:
                return m_Reader.ReadInt8(offset).ToString();

            case TypeCode.Byte:
            case TypeCode.Char:
                return m_Reader.ReadUInt8(offset).ToString();

            case TypeCode.Boolean:
                return (m_Reader.ReadUInt8(offset) != 0).ToString();

            default:
                throw new Exception($"Can't get value of {node.Type} type");
        }
    }

    Array ReadBasicTypeArray(TypeTreeNode node, long offset, int arraySize)
    {
        // bool isn't blittable into Array.CreateInstance(typeof(bool), ...) the way other basic types
        // are, so read into a byte buffer and convert.
        if (node.CSharpType == typeof(bool))
        {
            var tmpArray = new byte[arraySize];
            m_Reader.ReadArray(offset, arraySize * node.Size, tmpArray);
            return Array.ConvertAll(tmpArray, b => b != 0);
        }

        var array = Array.CreateInstance(node.CSharpType, arraySize);
        m_Reader.ReadArray(offset, arraySize * node.Size, array);
        return array;
    }
}
