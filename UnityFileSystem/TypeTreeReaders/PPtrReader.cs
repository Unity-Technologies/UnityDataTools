using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityDataTools.FileSystem.TypeTreeReaders
{
    // This class is used to extract all the PPtrs in a serialized object. It executes a callback whenever a PPtr is found.
    // It provides a string representing the property path of the property (e.g. "m_MyObject.m_MyArray[2].m_PPtrProperty").
    public class PPtrReader
    {
        private SerializedFile m_SerializedFile;
        UnityFileReader m_Reader;
        long m_Offset;

        Action<int, long, string> m_Callback;

        public PPtrReader(SerializedFile serializedFile, TypeTreeNode node, UnityFileReader reader, long offset, Action<int, long, string> callback)
        {
            m_SerializedFile = serializedFile;
            m_Reader = reader;
            m_Offset = offset;
            m_Callback = callback;

            foreach (var child in node.Children)
            {
                var sb = new StringBuilder(child.Name);
                ProcessNode(child, sb);
            }
        }

        private void ProcessNode(TypeTreeNode node, StringBuilder propertyPath)
        {
            if (node.IsBasicType)
            {
                m_Offset += node.Size;
            }
            else if (node.IsArray)
            {
                ProcessArray(node, propertyPath);
            }
            else if (node.Type.StartsWith("PPtr<"))
            {
                ExtractPPtr(propertyPath.ToString());
            }
            else if (node.CSharpType == typeof(string))
            {
                m_Offset += m_Reader.ReadInt32(m_Offset) + 4;
            }
            else if (node.IsManagedReferenceRegistry)
            {
                ProcessManagedReferenceRegistry(node, propertyPath);
            }
            else
            {
                foreach (var child in node.Children)
                {
                    var size = propertyPath.Length;
                    propertyPath.Append('.');
                    propertyPath.Append(child.Name);
                    ProcessNode(child, propertyPath);
                    propertyPath.Remove(size, propertyPath.Length - size);
                }
            }

            if (node.MetaFlags.HasFlag(TypeTreeMetaFlags.AlignBytes) || node.MetaFlags.HasFlag(TypeTreeMetaFlags.AnyChildUsesAlignBytes))
            {
                m_Offset = (m_Offset + 3) & ~(3);
            }
        }

        private void ProcessArray(TypeTreeNode node, StringBuilder propertyPath, bool isManagedReferenceRegistry = false)
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

                for (int i = 0;i < arraySize; ++i)
                {
                    var size = propertyPath.Length;
                    propertyPath.Append('[');
                    propertyPath.Append(i);
                    propertyPath.Append(']');
                    if (!isManagedReferenceRegistry)
                    {
                        ProcessNode(dataNode, propertyPath);
                    }
                    else
                    {
                        if (dataNode.Children.Count < 3)
                            throw new Exception("Invalid ReferencedObject");
                            
                        // First child is rid.
                        long rid = m_Reader.ReadInt64(m_Offset);
                        m_Offset += 8;
                        
                        ProcessManagedReferenceData(dataNode.Children[1], dataNode.Children[2], propertyPath);
                    }
                    propertyPath.Remove(size, propertyPath.Length - size);
                }
            }
        }

        private void ProcessManagedReferenceRegistry(TypeTreeNode node, StringBuilder propertyPath)
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

                while (ProcessManagedReferenceData(refTypeNode, refObjData, propertyPath))
                {}
            }
            else if (version == 2)
            {
                var refIdsVectorNode = node.Children[1];

                if (refIdsVectorNode.Children.Count < 1 || refIdsVectorNode.Name != "RefIds")
                    throw new Exception("Invalid ManagedReferenceRegistry RefIds vector");

                var refIdsArrayNode = refIdsVectorNode.Children[0];

                if (refIdsArrayNode.Children.Count != 2 || !refIdsArrayNode.Flags.HasFlag(TypeTreeFlags.IsArray))
                    throw new Exception("Invalid ManagedReferenceRegistry RefIds array");

                var size = propertyPath.Length;
                propertyPath.Append('.');
                propertyPath.Append("RefIds");
                ProcessArray(refIdsArrayNode, propertyPath, true);
                propertyPath.Remove(size, propertyPath.Length - size);
            }
            else
            {
                throw new Exception("Unsupported ManagedReferenceRegistry version");
            }
        }
        
        bool ProcessManagedReferenceData(TypeTreeNode refTypeNode, TypeTreeNode referencedTypeDataNode, StringBuilder propertyPath)
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
            
            if (className == "Terminus" && namespaceName == "UnityEngine.DMAT" && assemblyName == "FAKE_ASM")
                return false;

            var refTypeTypeTree = m_SerializedFile.GetRefTypeTypeTreeRoot(className, namespaceName, assemblyName);

            // Process the ReferencedObject using its own TypeTree.
            var size = propertyPath.Length;
            propertyPath.Append('.');
            propertyPath.Append("data");
            ProcessNode(refTypeTypeTree, propertyPath);
            propertyPath.Remove(size, propertyPath.Length - size);

            return true;
        }

        private void ExtractPPtr(string propertyPath)
        {
            var fileId = m_Reader.ReadInt32(m_Offset);
            m_Offset += 4;
            var pathId = m_Reader.ReadInt64(m_Offset);
            m_Offset += 8;

            if (fileId != 0 || pathId != 0)
            {
                m_Callback(fileId, pathId, propertyPath);
            }
        }
    }
}
