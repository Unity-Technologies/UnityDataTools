using System;
using System.IO;
using System.Text;
using UnityDataTools.FileSystem;

namespace UnityDataTools.TextDumper
{
    public class TextDumperTool
    {
        StringBuilder m_StringBuilder = new StringBuilder(1024);
        bool m_SkipLargeArrays;

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
                            using var writer = new StreamWriter(Path.Combine(outputPath, Path.GetFileName(node.Path) + ".txt"), false);
                            OutputSerializedFile("/" + node.Path, writer);
                        }
                    }
                }
                catch (NotSupportedException)
                {
                    // Try as SerializedFile
                    using var writer = new StreamWriter(Path.GetFileName(path) + ".txt", false);
                    OutputSerializedFile("/" + path, writer);
                }
            }
            catch
            {
                Console.WriteLine("Error opening file. Is it a SerializedFile or AssetBundle?");
                return 1;
            }

            return 0;
        }

        void RecursiveDump(TypeTreeNode node, UnityFileReader reader, StreamWriter writer, ref long offset, int level)
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
                    m_StringBuilder.Append(ReadValue(node, reader, offset));

                    offset += node.Size;
                }
                else if (node.Type == "string")
                {
                    var stringSize = reader.ReadInt32(offset);

                    m_StringBuilder.Append(reader.ReadString(offset + 4, stringSize));

                    offset += stringSize + 4;

                    // Skip child nodes as they were already processed here.
                    skipChildren = true;
                }

                writer.WriteLine(m_StringBuilder);
                m_StringBuilder.Clear();
            }
            else
            {
                // First child contains array size.
                var sizeNode = node.Children[0];

                if (sizeNode.Size != 4 || !sizeNode.IsLeaf)
                    throw new Exception("Invalid array size");

                var arraySize = reader.ReadInt32(offset);
                offset += 4;

                m_StringBuilder.Append(' ', level * 2);
                m_StringBuilder.Append(sizeNode.Name);
                m_StringBuilder.Append(": ");
                m_StringBuilder.Append(arraySize);

                writer.WriteLine(m_StringBuilder);
                m_StringBuilder.Clear();

                // Second child contains array type information.
                var dataNode = node.Children[1];

                if (arraySize > 0)
                {
                    if (dataNode.IsBasicType)
                    {
                        m_StringBuilder.Append(' ', level * 2);

                        if (arraySize > 256 && m_SkipLargeArrays)
                        {
                            m_StringBuilder.Append("<Skipped>");
                            offset += dataNode.Size * arraySize;
                        }
                        else
                        {
                            var array = ReadBasicTypeArray(dataNode, reader, offset, arraySize);
                            offset += dataNode.Size * arraySize;

                            m_StringBuilder.Append(array.GetValue(0));
                            for (int i = 1; i < arraySize; ++i)
                            {
                                m_StringBuilder.Append(", ");
                                m_StringBuilder.Append(array.GetValue(i));
                            }
                        }

                        writer.WriteLine(m_StringBuilder);
                        m_StringBuilder.Clear();
                    }
                    else
                    {
                        for (int i = 0; i < arraySize; ++i)
                        {
                            RecursiveDump(dataNode, reader, writer, ref offset, level);
                        }
                    }
                }

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
                    RecursiveDump(child, reader, writer, ref offset, level + 1);
                }
            }
        }

        void OutputSerializedFile(string path, StreamWriter writer)
        {
            using (var reader = new UnityFileReader(path, 64 * 1024 * 1024))
            using (var sf = UnityFileSystem.OpenSerializedFile(path))
            {
                var i = 1;

                writer.WriteLine("External References");
                foreach (var extRef in sf.ExternalReferences)
                {
                    writer.WriteLine($"path({i}): \"{extRef.Path}\" GUID: {extRef.Guid} Type: {(int)extRef.Type}");
                    ++i;
                }
                writer.WriteLine();

                foreach (var obj in sf.Objects)
                {
                    var root = sf.GetTypeTreeRoot(obj.Id);
                    var offset = obj.Offset;

                    writer.Write($"ID: {obj.Id} (ClassID: {obj.TypeId}) ");
                    RecursiveDump(root, reader, writer, ref offset, 0);
                    writer.WriteLine();
                }
            }
        }

        string ReadValue(TypeTreeNode node, UnityFileReader reader, long offset)
        {
            switch (Type.GetTypeCode(node.CSharpType))
            {
                case TypeCode.Int32:
                    return reader.ReadInt32(offset).ToString();

                case TypeCode.UInt32:
                    return reader.ReadUInt32(offset).ToString();

                case TypeCode.Single:
                    return reader.ReadFloat(offset).ToString();

                case TypeCode.Double:
                    return reader.ReadDouble(offset).ToString();

                case TypeCode.Int16:
                    return reader.ReadInt16(offset).ToString();

                case TypeCode.UInt16:
                    return reader.ReadUInt16(offset).ToString();

                case TypeCode.Int64:
                    return reader.ReadInt64(offset).ToString();

                case TypeCode.UInt64:
                    return reader.ReadUInt64(offset).ToString();

                case TypeCode.SByte:
                    return reader.ReadUInt8(offset).ToString();

                case TypeCode.Byte:
                case TypeCode.Char:
                    return reader.ReadUInt8(offset).ToString();

                case TypeCode.Boolean:
                    return (reader.ReadUInt8(offset) != 0).ToString();

                default:
                    throw new Exception($"Can't get value of {node.Type} type");
            }
        }

        Array ReadBasicTypeArray(TypeTreeNode node, UnityFileReader reader, long offset, int arraySize)
        {
            // Special case for boolean arrays.
            if (node.CSharpType == typeof(bool))
            {
                var tmpArray = new byte[arraySize];
                var boolArray = new bool[arraySize];

                reader.ReadArray(offset + 4, arraySize * node.Size, tmpArray);

                for (int i = 0; i < arraySize; ++i)
                {
                    boolArray[i] = tmpArray[i] != 0;
                }

                return boolArray;
            }
            else
            {
                var array = Array.CreateInstance(node.CSharpType, arraySize);

                reader.ReadArray(offset + 4, arraySize * node.Size, array);

                return array;
            }
        }
    }
}
