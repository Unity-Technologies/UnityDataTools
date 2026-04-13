using System;
using System.Text;
using Force.Crc32;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer;

/// <summary>
/// Walks serialized object TypeTrees to extract PPtr references and compute a rolling CRC32.
/// External stream segments (StreamingInfo / StreamedResource) extend the CRC using offset, size, and path only,
/// avoiding full reads of large companion .resS data.
/// </summary>
public class PPtrAndCrcProcessor : IDisposable
{
    public delegate int CallbackDelegate(long objectId, int fileId, long pathId, string propertyPath, string propertyType);

    private SerializedFile m_SerializedFile;
    private UnityFileReader m_Reader;
    private long m_Offset;
    private long m_ObjectId;
    private uint m_Crc32;
    private StringBuilder m_StringBuilder = new();
    private byte[] m_pptrBytes = new byte[4];

    private CallbackDelegate m_Callback;

    public PPtrAndCrcProcessor(SerializedFile serializedFile, UnityFileReader reader, CallbackDelegate callback)
    {
        m_SerializedFile = serializedFile;
        m_Reader = reader;
        m_Callback = callback;
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// Extends CRC32 with a stable fingerprint for an external stream segment without reading blob bytes.
    /// </summary>
    private static uint AppendExternalStreamFingerprint(uint crc32, long offset, int size, string filename)
    {
        crc32 = Crc32Algorithm.Append(crc32, BitConverter.GetBytes(offset));
        crc32 = Crc32Algorithm.Append(crc32, BitConverter.GetBytes(size));
        crc32 = Crc32Algorithm.Append(crc32, Encoding.UTF8.GetBytes(filename));
        return crc32;
    }

    public uint Process(long objectId, long offset, TypeTreeNode node)
    {
        m_Offset = offset;
        m_ObjectId = objectId;
        m_Crc32 = 0;

        foreach (var child in node.Children)
        {
            m_StringBuilder.Clear();
            m_StringBuilder.Append(child.Name);
            ProcessNode(child, false);
        }

        return m_Crc32;
    }

    private void ProcessNode(TypeTreeNode node, bool isInManagedReferenceRegistry)
    {
        if (node.IsBasicType)
        {
            m_Crc32 = m_Reader.ComputeCRC(m_Offset, node.Size, m_Crc32);
            m_Offset += node.Size;
        }
        else if (node.IsArray)
        {
            ProcessArray(node, false, isInManagedReferenceRegistry);
        }
        else if (node.Type == "vector" || node.Type == "map" || node.Type == "staticvector")
        {
            ProcessArray(node.Children[0], false, isInManagedReferenceRegistry);
        }
        else if (node.Type.StartsWith("PPtr<"))
        {
            var startIndex = node.Type.IndexOf('<') + 1;
            var endIndex = node.Type.Length - 1;
            var referencedType = node.Type.Substring(startIndex, endIndex - startIndex);

            ExtractPPtr(referencedType);
        }
        else if (node.Type == "StreamingInfo")
        {
            if (node.Children.Count != 3)
                throw new Exception("Invalid StreamingInfo");

            var streamOffset = node.Children[0].Size == 4 ? m_Reader.ReadInt32(m_Offset) : m_Reader.ReadInt64(m_Offset);
            m_Offset += node.Children[0].Size;

            var size = m_Reader.ReadInt32(m_Offset);
            m_Offset += 4;

            var stringSize = m_Reader.ReadInt32(m_Offset);
            var filename = m_Reader.ReadString(m_Offset + 4, stringSize);
            m_Offset += stringSize + 4;
            m_Offset = (m_Offset + 3) & ~(3);

            if (size > 0)
            {
                m_Crc32 = AppendExternalStreamFingerprint(m_Crc32, streamOffset, size, filename);
            }
        }
        else if (node.Type == "StreamedResource")
        {
            if (node.Children.Count != 3)
                throw new Exception("Invalid StreamedResource");

            var stringSize = m_Reader.ReadInt32(m_Offset);
            var filename = m_Reader.ReadString(m_Offset + 4, stringSize);
            m_Offset += stringSize + 4;
            m_Offset = (m_Offset + 3) & ~(3);

            var offset = m_Reader.ReadInt64(m_Offset);
            m_Offset += 8;

            var size = (int)m_Reader.ReadInt64(m_Offset);
            m_Offset += 8;

            if (size > 0)
            {
                m_Crc32 = AppendExternalStreamFingerprint(m_Crc32, offset, size, filename);
            }
        }
        else if (node.CSharpType == typeof(string))
        {
            var prevOffset = m_Offset;
            m_Offset += m_Reader.ReadInt32(m_Offset) + 4;
            m_Crc32 = m_Reader.ComputeCRC(prevOffset, (int)(m_Offset - prevOffset), m_Crc32);
        }
        else if (node.IsManagedReferenceRegistry)
        {
            // ManagedReferenceRegistry are never nested
            if (!isInManagedReferenceRegistry)
                ProcessManagedReferenceRegistry(node);
        }
        else
        {
            foreach (var child in node.Children)
            {
                var size = m_StringBuilder.Length;
                m_StringBuilder.Append('.');
                m_StringBuilder.Append(child.Name);
                ProcessNode(child, isInManagedReferenceRegistry);
                m_StringBuilder.Remove(size, m_StringBuilder.Length - size);
            }
        }

        if (
                ((int)node.MetaFlags & (int)TypeTreeMetaFlags.AlignBytes) != 0 ||
                ((int)node.MetaFlags & (int)TypeTreeMetaFlags.AnyChildUsesAlignBytes) != 0
            )
        {
            m_Offset = (m_Offset + 3) & ~(3);
        }
    }

    private void ProcessArray(TypeTreeNode node, bool isManagedReferenceRegistry, bool isInManagedReferenceRegistry)
    {
        var dataNode = node.Children[1];

        if (dataNode.IsBasicType)
        {
            var arraySize = m_Reader.ReadInt32(m_Offset);
            m_Crc32 = m_Reader.ComputeCRC(m_Offset, dataNode.Size * arraySize + 4, m_Crc32);
            m_Offset += dataNode.Size * arraySize + 4;
        }
        else
        {
            m_Crc32 = m_Reader.ComputeCRC(m_Offset, 4, m_Crc32);
            var arraySize = m_Reader.ReadInt32(m_Offset);
            m_Offset += 4;

            for (int i = 0; i < arraySize; ++i)
            {
                if (!isManagedReferenceRegistry)
                {
                    var size = m_StringBuilder.Length;
                    m_StringBuilder.Append('[');
                    m_StringBuilder.Append(i);
                    m_StringBuilder.Append(']');

                    ProcessNode(dataNode, isInManagedReferenceRegistry);

                    m_StringBuilder.Remove(size, m_StringBuilder.Length - size);
                }
                else
                {
                    if (dataNode.Children.Count < 3)
                        throw new Exception("Invalid ReferencedObject");

                    // First child is rid.
                    long rid = m_Reader.ReadInt64(m_Offset);
                    m_Crc32 = m_Reader.ComputeCRC(m_Offset, 8, m_Crc32);
                    m_Offset += 8;

                    ProcessManagedReferenceData(dataNode.Children[1], dataNode.Children[2], rid);
                }
            }
        }
    }

    private void ProcessManagedReferenceRegistry(TypeTreeNode node)
    {
        if (node.Children.Count < 2)
            throw new Exception("Invalid ManagedReferenceRegistry");

        // First child is version number.
        var version = m_Reader.ReadInt32(m_Offset);
        m_Crc32 = m_Reader.ComputeCRC(m_Offset, node.Children[0].Size, m_Crc32);
        m_Offset += node.Children[0].Size;

        if (version == 1)
        {
            // Second child is the ReferencedObject.
            var refObjNode = node.Children[1];
            // And its children are the referenced type and data nodes.
            var refTypeNode = refObjNode.Children[0];
            var refObjData = refObjNode.Children[1];

            int i = 0;
            while (ProcessManagedReferenceData(refTypeNode, refObjData, i++))
            {
            }
        }
        else if (version == 2)
        {
            var refIdsVectorNode = node.Children[1];

            if (refIdsVectorNode.Children.Count < 1 || refIdsVectorNode.Name != "RefIds")
                throw new Exception("Invalid ManagedReferenceRegistry RefIds vector");

            var refIdsArrayNode = refIdsVectorNode.Children[0];

            if (refIdsArrayNode.Children.Count != 2 || !refIdsArrayNode.IsArray)
                throw new Exception("Invalid ManagedReferenceRegistry RefIds array");

            var size = m_StringBuilder.Length;
            m_StringBuilder.Append('.');
            m_StringBuilder.Append("RefIds");
            ProcessArray(refIdsArrayNode, true, true);
            m_StringBuilder.Remove(size, m_StringBuilder.Length - size);
        }
        else
        {
            throw new Exception($"Unsupported ManagedReferenceRegistry version {version}");
        }
    }

    bool ProcessManagedReferenceData(TypeTreeNode refTypeNode, TypeTreeNode referencedTypeDataNode, long rid)
    {
        if (refTypeNode.Children.Count < 3)
            throw new Exception("Invalid ReferencedManagedType");

        var stringSize = m_Reader.ReadInt32(m_Offset);
        m_Crc32 = m_Reader.ComputeCRC(m_Offset, stringSize + 4, m_Crc32);
        var className = m_Reader.ReadString(m_Offset + 4, stringSize);
        m_Offset += stringSize + 4;
        m_Offset = (m_Offset + 3) & ~(3);

        stringSize = m_Reader.ReadInt32(m_Offset);
        m_Crc32 = m_Reader.ComputeCRC(m_Offset, stringSize + 4, m_Crc32);
        var namespaceName = m_Reader.ReadString(m_Offset + 4, stringSize);
        m_Offset += stringSize + 4;
        m_Offset = (m_Offset + 3) & ~(3);

        stringSize = m_Reader.ReadInt32(m_Offset);
        m_Crc32 = m_Reader.ComputeCRC(m_Offset, stringSize + 4, m_Crc32);
        var assemblyName = m_Reader.ReadString(m_Offset + 4, stringSize);
        m_Offset += stringSize + 4;
        m_Offset = (m_Offset + 3) & ~(3);

        if ((className == "Terminus" && namespaceName == "UnityEngine.DMAT" && assemblyName == "FAKE_ASM") ||
            rid == -1 || rid == -2)
        {
            return false;
        }

        var refTypeTypeTree = m_SerializedFile.GetRefTypeTypeTreeRoot(className, namespaceName, assemblyName);

        // Process the ReferencedObject using its own TypeTree.
        var size = m_StringBuilder.Length;
        m_StringBuilder.Append("rid(");
        m_StringBuilder.Append(rid);
        m_StringBuilder.Append(").data");
        ProcessNode(refTypeTypeTree, true);
        m_StringBuilder.Remove(size, m_StringBuilder.Length - size);

        return true;
    }

    private void ExtractPPtr(string referencedType)
    {
        var fileId = m_Reader.ReadInt32(m_Offset);
        m_Offset += 4;
        var pathId = m_Reader.ReadInt64(m_Offset);
        m_Offset += 8;

        if (fileId != 0 || pathId != 0)
        {
            var refId = m_Callback(m_ObjectId, fileId, pathId, m_StringBuilder.ToString(), referencedType);
            m_pptrBytes[0] = (byte)(refId >> 24);
            m_pptrBytes[1] = (byte)(refId >> 16);
            m_pptrBytes[2] = (byte)(refId >> 8);
            m_pptrBytes[3] = (byte)(refId);
            m_Crc32 = Crc32Algorithm.Append(m_Crc32, m_pptrBytes);
        }
    }
}
