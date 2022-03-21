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
        UnityFileReader m_Reader;
        long m_Offset;

        Action<int, long, string> m_Callback;

        public PPtrReader(TypeTreeNode node, UnityFileReader reader, long offset, Action<int, long, string> callback)
        {
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
                        ProcessNode(dataNode, propertyPath);
                        propertyPath.Remove(size, propertyPath.Length - size);
                    }
                }
            }
            else if (node.Type.StartsWith("PPtr<"))
            {
                ExtractPPtr(node, propertyPath.ToString());
            }
            else if (node.CSharpType == typeof(string))
            {
                m_Offset += m_Reader.ReadInt32(m_Offset) + 4;
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

        private void ExtractPPtr(TypeTreeNode node, string propertyPath)
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
