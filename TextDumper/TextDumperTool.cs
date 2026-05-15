using System;
using System.IO;
using System.Text;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.TextDumper;

public class TextDumperTool
{
    StringBuilder m_StringBuilder = new StringBuilder(1024);
    bool m_SkipLargeArrays;
    UnityFileReader m_Reader;
    SerializedFile m_SerializedFile;
    TextWriter m_Writer;

    public enum DumpFormat
    {
        Text,
    }

    public class DumpOptions
    {
        public DumpFormat Format { get; init; } = DumpFormat.Text;
        public string Path { get; init; }
        public string OutputPath { get; init; }
        public bool SkipLargeArrays { get; init; }
        public long ObjectId { get; init; }
        public string TypeFilter { get; init; }
        public bool ToStdout { get; init; }
    }

    public int Dump(DumpOptions options)
    {
        m_SkipLargeArrays = options.SkipLargeArrays;

        try
        {
            if (!File.Exists(options.Path))
            {
                Console.Error.WriteLine($"Error: File not found: {options.Path}");
                return 1;
            }

            if (ArchiveDetector.IsUnityArchive(options.Path))
                return DumpArchive(options);

            if (YamlSerializedFileDetector.IsYamlSerializedFile(options.Path))
            {
                Console.Error.WriteLine("Error: The file is a YAML-format SerializedFile, which is not supported.");
                Console.Error.WriteLine("UnityDataTool only supports binary-format SerializedFiles.");
                return 1;
            }

            if (SerializedFileDetector.TryDetectSerializedFile(options.Path, out _))
                return DumpSerializedFile(options);

            Console.Error.WriteLine("Error: The file does not appear to be a valid Unity SerializedFile or Unity Archive.");
            Console.Error.WriteLine($"File: {options.Path}");
            return 1;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: {e.GetType()}: {e.Message}");
            Console.Error.WriteLine(e.StackTrace);
            return 1;
        }
    }

    int DumpSerializedFile(DumpOptions options)
    {
        try
        {
            if (options.ToStdout)
            {
                m_Writer = Console.Out;
                OutputSerializedFile(options.Path, options);
                m_Writer.Flush();
            }
            else
            {
                using var writer = new StreamWriter(Path.Combine(options.OutputPath, Path.GetFileName(options.Path) + ".txt"), false);
                m_Writer = writer;
                OutputSerializedFile(options.Path, options);
            }
        }
        catch (SerializedFileOpenException)
        {
            var hint = SerializedFileDetector.GetOpenFailureHint(options.Path);
            if (hint != null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(hint);
            }
            return 1;
        }

        return 0;
    }

    // For convenience we also support directly dumping serialized files that are inside an archive,
    // so that it's not necessary to use `archive extract` if you only want to see values from the object serialization.
    int DumpArchive(DumpOptions options)
    {
        using var archive = UnityFileSystem.MountArchive(options.Path, "/");

        if (options.ToStdout)
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
            m_Writer = Console.Out;
            OutputSerializedFile("/" + node2.Path, options);
            m_Writer.Flush();
        }
        else
        {
            foreach (var node in archive.Nodes)
            {
                Console.WriteLine($"Processing {node.Path} {node.Size} {node.Flags}");

                if (node.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                {
                    using var writer = new StreamWriter(Path.Combine(options.OutputPath, Path.GetFileName(node.Path) + ".txt"), false);
                    m_Writer = writer;
                    OutputSerializedFile("/" + node.Path, options);
                }
            }
        }

        return 0;
    }

    void OutputSerializedFile(string path, DumpOptions options)
    {
        var objectId = options.ObjectId;
        var typeFilter = string.IsNullOrWhiteSpace(options.TypeFilter) ? null : options.TypeFilter;
        bool filtered = objectId != 0 || typeFilter != null;

        int filterTypeId = 0;
        bool filterByTypeId = typeFilter != null && int.TryParse(typeFilter, out filterTypeId);

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

                var root = m_SerializedFile.GetTypeTreeRoot(obj.Id);

                if (typeFilter != null)
                {
                    if (filterByTypeId)
                    {
                        if (obj.TypeId != filterTypeId)
                            continue;
                    }
                    else
                    {
                        var typeName = TypeIdRegistry.GetTypeName(obj.TypeId);
                        // GetTypeName returns the id as a string when the type is unknown;
                        // fall back to the TypeTree root node for script types.
                        if (typeName == obj.TypeId.ToString())
                            typeName = root.Type;
                        if (!string.Equals(typeName, typeFilter, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                }

                var offset = obj.Offset;

                m_Writer.Write($"ID: {obj.Id} (ClassID: {obj.TypeId}) ");
                RecursiveDump(root, ref offset, 0);
                m_Writer.WriteLine();
                dumpedObject = true;
            }

            if ((objectId != 0 || typeFilter != null) && !dumpedObject)
            {
                if (objectId != 0)
                    m_Writer.WriteLine($"Object with ID {objectId} not found.");
                else
                    m_Writer.WriteLine($"No objects found matching type \"{typeFilter}\".");
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
            m_StringBuilder.Append(' ', level * 2);

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

            // Basic data type.
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
            offset = (offset + 3) & ~(3);
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

        m_StringBuilder.Append(' ', level * 2);
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
                m_StringBuilder.Append(' ', (level + 1) * 2);

                if (arraySize > 256 && m_SkipLargeArrays)
                {
                    m_StringBuilder.Append("<Skipped>");
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

        m_StringBuilder.Append(' ', level * 2);
        m_StringBuilder.Append($"rid(");
        m_StringBuilder.Append(id);
        m_StringBuilder.Append(") ReferencedObject");

        m_Writer.WriteLine(m_StringBuilder);
        m_StringBuilder.Clear();

        ++level;

        var refTypeOffset = offset;
        var stringSize = m_Reader.ReadInt32(offset);
        var className = m_Reader.ReadString(offset + 4, stringSize);
        offset += stringSize + 4;
        offset = (offset + 3) & ~(3);

        stringSize = m_Reader.ReadInt32(offset);
        var namespaceName = m_Reader.ReadString(offset + 4, stringSize);
        offset += stringSize + 4;
        offset = (offset + 3) & ~(3);

        stringSize = m_Reader.ReadInt32(offset);
        var assemblyName = m_Reader.ReadString(offset + 4, stringSize);
        offset += stringSize + 4;
        offset = (offset + 3) & ~(3);

        if (className == "Terminus" && namespaceName == "UnityEngine.DMAT" && assemblyName == "FAKE_ASM")
            return false;

        // Not the most efficient way, but it simplifies the code.
        RecursiveDump(refTypeNode, ref refTypeOffset, level);

        m_StringBuilder.Append(' ', level * 2);
        m_StringBuilder.Append(referencedTypeDataNode.Name);
        m_StringBuilder.Append(' ');
        m_StringBuilder.Append(referencedTypeDataNode.Type);
        m_StringBuilder.Append(' ');

        m_Writer.WriteLine(m_StringBuilder);
        m_StringBuilder.Clear();

        if (id == -1 || id == -2)
        {
            m_StringBuilder.Append(' ', level * 2);
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
                return m_Reader.ReadUInt8(offset).ToString();

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
        // Special case for boolean arrays.
        if (node.CSharpType == typeof(bool))
        {
            var tmpArray = new byte[arraySize];
            var boolArray = new bool[arraySize];

            m_Reader.ReadArray(offset, arraySize * node.Size, tmpArray);

            for (int i = 0; i < arraySize; ++i)
            {
                boolArray[i] = tmpArray[i] != 0;
            }

            return boolArray;
        }
        else
        {
            var array = Array.CreateInstance(node.CSharpType, arraySize);

            m_Reader.ReadArray(offset, arraySize * node.Size, array);

            return array;
        }
    }
}
