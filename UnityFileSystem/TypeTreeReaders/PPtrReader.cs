using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityDataTools.FileSystem.TypeTreeReaders;

// This class is used to extract all the PPtrs in a serialized object. It executes a callback whenever a PPtr is found.
// It provides a string representing the property path of the property (e.g. "m_MyObject.m_MyArray[2].m_PPtrProperty").
public class PPtrReader
{
    public delegate void CallbackDelegate(long objectId, int fileId, long pathId, string propertyPath, string propertyType);
    
    private SerializedFile m_SerializedFile;
    private UnityFileReader m_Reader;
    private long m_Offset;
    private long m_ObjectId;
    private StringBuilder m_StringBuilder = new();
    
    private CallbackDelegate m_Callback;

    public PPtrReader(SerializedFile serializedFile, UnityFileReader reader,
        CallbackDelegate callback)
    {
        m_SerializedFile = serializedFile;
        m_Reader = reader;
        m_Callback = callback;
    }

    public void Process(long objectId, long offset, TypeTreeNode node)
    {
        m_Offset = offset;
        m_ObjectId = objectId;

        foreach (var child in node.Children)
        {
            m_StringBuilder.Clear();
            m_StringBuilder.Append(child.Name);
            ProcessNode(child);
        }
    }

    private void ProcessNode(TypeTreeNode node)
    {
        if (node.IsBasicType)
        {
            m_Offset += node.Size;
        }
        else if (node.IsArray)
        {
            ProcessArray(node);
        }
        else if (node.Type == "vector" || node.Type == "map" || node.Type == "staticvector")
        {
            ProcessArray(node.Children[0]);
        }
        else if (node.Type.StartsWith("PPtr<"))
        {
            var startIndex = node.Type.IndexOf('<') + 1;
            var endIndex = node.Type.Length - 1;
            var referencedType = node.Type.Substring(startIndex, endIndex - startIndex);
            
            ExtractPPtr(referencedType);
        }
        else if (node.CSharpType == typeof(string))
        {
            m_Offset += m_Reader.ReadInt32(m_Offset) + 4;
        }
        else if (node.IsManagedReferenceRegistry)
        {
            ProcessManagedReferenceRegistry(node);
        }
        else
        {
            foreach (var child in node.Children)
            {
                var size = m_StringBuilder.Length;
                m_StringBuilder.Append('.');
                m_StringBuilder.Append(child.Name);
                ProcessNode(child);
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

    private void ProcessArray(TypeTreeNode node, bool isManagedReferenceRegistry = false)
    {
        var dataNode = node.Children[1];

        if (dataNode.IsBasicType)
        {
            var arraySize = m_Reader.ReadInt32(m_Offset);
            m_Offset += dataNode.Size * arraySize + 4;
        }
        else
        {
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
                    
                    ProcessNode(dataNode);
                    
                    m_StringBuilder.Remove(size, m_StringBuilder.Length - size);
                }
                else
                {
                    if (dataNode.Children.Count < 3)
                        throw new Exception("Invalid ReferencedObject");

                    // First child is rid.
                    long rid = m_Reader.ReadInt64(m_Offset);
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
            ProcessArray(refIdsArrayNode, true);
            m_StringBuilder.Remove(size, m_StringBuilder.Length - size);
        }
        else
        {
            throw new Exception("Unsupported ManagedReferenceRegistry version");
        }
    }

    bool ProcessManagedReferenceData(TypeTreeNode refTypeNode, TypeTreeNode referencedTypeDataNode, long rid)
    {
        if (refTypeNode.Children.Count < 3)
            throw new Exception("Invalid ReferencedManagedType");

        var stringSize = m_Reader.ReadInt32(m_Offset);
        var className = m_Reader.ReadString(m_Offset + 4, stringSize);
        m_Offset += stringSize + 4;
        m_Offset = (m_Offset + 3) & ~(3);

        stringSize = m_Reader.ReadInt32(m_Offset);
        var namespaceName = m_Reader.ReadString(m_Offset + 4, stringSize);
        m_Offset += stringSize + 4;
        m_Offset = (m_Offset + 3) & ~(3);

        stringSize = m_Reader.ReadInt32(m_Offset);
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
        ProcessNode(refTypeTypeTree);
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
            m_Callback(m_ObjectId, fileId, pathId, m_StringBuilder.ToString(), referencedType);
        }
    }
}