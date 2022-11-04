using System;
using System.IO;
using System.Text;
using UnityDataTools.FileSystem;

namespace UnityDataTools.TextDumper;

public class TextDumperTool
{
    StringBuilder m_StringBuilder = new StringBuilder(1024);
    bool m_SkipLargeArrays;
    UnityFileReader m_Reader;
    SerializedFile m_SerializedFile;
    StreamWriter m_Writer;

    public int Dump(string path, string outputPath, bool skipLargeArrays)
    {
        m_SkipLargeArrays = skipLargeArrays;

        try
        {
            try
            {
                using var archive = UnityFileSystem.MountArchive(path, "/");
                foreach (var node in archive.Nodes)
                {
                    Console.WriteLine($"Processing {node.Path} {node.Size} {node.Flags}");

                    if (node.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                    {
                        using (m_Writer = new StreamWriter(Path.Combine(outputPath, Path.GetFileName(node.Path) + ".txt"), false))
                        {
                            OutputSerializedFile("/" + node.Path);
                        }
                    }
                }
            }
            catch (NotSupportedException)
            {
                // Try as SerializedFile
                using (m_Writer = new StreamWriter(Path.GetFileName(path) + ".txt", false))
                {
                    OutputSerializedFile("/" + path);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error!");
            Console.Write($"{e.GetType()}: ");
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            return 1;
        }

        return 0;
    }

    void RecursiveDump(TypeTreeNode node, ref long offset, int level)
    {
        bool skipChildren = false;
            
        if (!node.IsArray)
        {
            m_StringBuilder.Append(' ', level * 2);

            // Name is useless for the root.
            if (level != 0)
            {
                m_StringBuilder.Append(node.Name);
                m_StringBuilder.Append(' ');
            }

            m_StringBuilder.Append(node.Type);
            m_StringBuilder.Append(' ');

            // Basic data type.
            if (node.IsBasicType)
            {
                m_StringBuilder.Append(ReadValue(node, offset));

                offset += node.Size;
            }
            else if (node.Type == "string")
            {
                var stringSize = m_Reader.ReadInt32(offset);

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
        else
        {
            DumpArray(node, ref offset, level);

            // Skip child nodes as they were already processed here.
            skipChildren = true;
        }

        if ((node.MetaFlags.HasFlag(TypeTreeMetaFlags.AlignBytes) || node.MetaFlags.HasFlag(TypeTreeMetaFlags.AnyChildUsesAlignBytes)))
        {
            offset = (offset + 3) & ~(3);
        }

        if (!skipChildren)
        {
            foreach (var child in node.Children)
            {
                RecursiveDump(child, ref offset, level + 1);
            }
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
        m_StringBuilder.Append("Array [Size=");
        m_StringBuilder.Append(arraySize);
        m_StringBuilder.Append(" Type=");
        m_StringBuilder.Append(dataNode.Type);
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
                    RecursiveDump(dataNode, ref offset, level);
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
            {}
        }
        else if (version == 2)
        {
            // Second child is the RefIds vector.
            var refIdsVectorNode = node.Children[1];

            if (refIdsVectorNode.Children.Count < 1 || refIdsVectorNode.Name != "RefIds")
                throw new Exception("Invalid ManagedReferenceRegistry RefIds vector");

            var refIdsArrayNode = refIdsVectorNode.Children[0];

            if (refIdsArrayNode.Children.Count != 2 || !refIdsArrayNode.Flags.HasFlag(TypeTreeFlags.IsArray))
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
            throw new Exception("Unsupported ManagedReferenceRegistry version");
        }
    }

    bool DumpManagedReferenceData(TypeTreeNode refTypeNode, TypeTreeNode referencedTypeDataNode, ref long offset, int level, long id)
    {
        if (refTypeNode.Children.Count < 3)
            throw new Exception("Invalid ReferencedManagedType");
            
        m_StringBuilder.Append(' ', level * 2);
        m_StringBuilder.Append($"rid_");
        m_StringBuilder.Append(id);
        m_StringBuilder.Append(" ReferencedObject");
        m_StringBuilder.AppendLine();
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

        var refTypeRoot = m_SerializedFile.GetRefTypeTypeTreeRoot(className, namespaceName, assemblyName);
                
        // Dump the ReferencedObject using its own TypeTree, but skip the root.
        foreach (var child in refTypeRoot.Children)
        {
            RecursiveDump(child, ref offset, level + 1);
        }

        return true;
    }

    void OutputSerializedFile(string path)
    {
        using (m_Reader = new UnityFileReader(path, 64 * 1024 * 1024))
        using (m_SerializedFile = UnityFileSystem.OpenSerializedFile(path))
        {
            var i = 1;

            m_Writer.WriteLine("External References");
            foreach (var extRef in m_SerializedFile.ExternalReferences)
            {
                m_Writer.WriteLine($"path({i}): \"{extRef.Path}\" GUID: {extRef.Guid} Type: {(int)extRef.Type}");
                ++i;
            }
            m_Writer.WriteLine();

            foreach (var obj in m_SerializedFile.Objects)
            {
                var root = m_SerializedFile.GetTypeTreeRoot(obj.Id);
                var offset = obj.Offset;

                m_Writer.Write($"ID: {obj.Id} (ClassID: {obj.TypeId}) ");
                RecursiveDump(root, ref offset, 0);
                m_Writer.WriteLine();
            }
        }
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