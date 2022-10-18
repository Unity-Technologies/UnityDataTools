using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityDataTools.FileSystem.TypeTreeReaders
{
    // This class should only be used when accessing specific properties of a serialized object and when the object
    // structure is already known. If all the properties of an object must be accessed, the TypeTreeNode should be
    // used instead (see the TextDumper library).
    //
    // Typical usage: randomAccessReader["prop"]["subProp"].GetValue<int>()
    // See the Processors in the Analyzer library for more examples.
    //
    // This class is optimized to read the least amount of data from the file when accessing properties of a serialized
    // object. It is required because the TypeTree doesn't provide the size of the serialized data when it is
    // variable (e.g. arrays). When accessing a property using this class, the offset of the property in the file is
    // determined by calculating the size of the data that was serialized before it.
    public class RandomAccessReader : IEnumerable<RandomAccessReader>
    {
        SerializedFile m_SerializedFile;
        UnityFileReader m_Reader;
        RandomAccessReader m_LastCachedChild = null;
        Lazy<int> m_Size;
        object m_Value = null;
        Dictionary<string, RandomAccessReader> m_childrenCacheObject;
        List<RandomAccessReader> m_childrenCacheArray;

        public int Size => m_Size.Value;
        public long Offset { get; }

        public bool IsObject => !TypeTreeNode.IsLeaf && !TypeTreeNode.IsBasicType && !TypeTreeNode.IsArray;
        public bool IsArrayOfObjects => TypeTreeNode.IsArray && !TypeTreeNode.Children[1].IsBasicType;
        public bool IsInManagedReferenceRegistry { get; }
        public TypeTreeNode TypeTreeNode { get; }

        public RandomAccessReader(SerializedFile serializedFile, TypeTreeNode node, UnityFileReader reader, long offset, bool isInManagedReferenceRegistry = false)
        {
            m_SerializedFile = serializedFile;
            IsInManagedReferenceRegistry = isInManagedReferenceRegistry;
            
            // Special case for vector and map objects, they always have a single Array child so we skip it.
            if (node.Type == "vector" || node.Type == "map")
            {
                TypeTreeNode = node.Children[0];
            }
            else
            {
                TypeTreeNode = node;
            }

            m_Reader = reader;
            Offset = offset;

            if (IsObject)
            {
                m_childrenCacheObject = new Dictionary<string, RandomAccessReader>();

                if (IsInManagedReferenceRegistry && TypeTreeNode.Type == "ReferencedObject")
                {
                    var referencedManagedType = GetChild("type");
                    var refTypeRoot = m_SerializedFile.GetRefTypeTypeTreeRoot(
                        referencedManagedType["class"].GetValue<string>(),
                        referencedManagedType["ns"].GetValue<string>(),
                        referencedManagedType["asm"].GetValue<string>());

                    // Manually create and cache a reader for the referenced type data, using its own TypeTree.
                    var refTypeDataReader = new RandomAccessReader(m_SerializedFile, refTypeRoot, reader, referencedManagedType.Offset + referencedManagedType.Size);
                    m_childrenCacheObject["data"] = refTypeDataReader;
                }
            }

            m_Size = new Lazy<int>(GetSize);
        }

        public bool HasChild(string name)
        {
            // This was faster than using a Dictionary.
            return TypeTreeNode.Children.Find(n => n.Name == name) != null;
        }

        int GetSize()
        {
            int size;

            if (TypeTreeNode.IsBasicType)
            {
                size = TypeTreeNode.Size;
            }
            else if (TypeTreeNode.IsArray)
            {
                var dataNode = TypeTreeNode.Children[1];

                if (dataNode.IsBasicType)
                {
                    var arraySize = m_Reader.ReadInt32(Offset);
                    size = dataNode.Size * arraySize;
                    size += 4;
                }
                else
                {
                    var arraySize = GetArraySize();
                    if (arraySize > 0)
                    {
                        if (dataNode.HasConstantSize)
                        {
                            size = GetArrayElement(0).Size * arraySize;
                            size += 4;
                        }
                        else
                        {
                            var lastArrayElement = GetArrayElement(arraySize - 1);
                            size = (int)(lastArrayElement.Offset + lastArrayElement.Size - Offset);
                        }
                    }
                    else
                    {
                        size = 4;
                    }
                }
            }
            else if (TypeTreeNode.CSharpType == typeof(string))
            {
                size = m_Reader.ReadInt32(Offset) + 4;
            }
            else
            {
                var lastChild = GetChild(TypeTreeNode.Children.Last().Name);
                size = (int)(lastChild.Offset + lastChild.Size - Offset);
            }

            if (TypeTreeNode.MetaFlags.HasFlag(TypeTreeMetaFlags.AlignBytes) || TypeTreeNode.MetaFlags.HasFlag(TypeTreeMetaFlags.AnyChildUsesAlignBytes))
            {
                var endOffset = (Offset + size + 3) & ~(3);

                size = (int)(endOffset - Offset);
            }

            return size;
        }

        RandomAccessReader GetChild(string name)
        {
            if (m_childrenCacheObject == null)
                throw new InvalidOperationException("Node is not an object");

            RandomAccessReader nodeReader = null;

            if (m_childrenCacheObject.TryGetValue(name, out nodeReader))
                return nodeReader;

            long offset;
            if (m_LastCachedChild == null)
            {
                offset = Offset;
            }
            else
            {
                offset = m_LastCachedChild.Offset + m_LastCachedChild.Size;
            }

            for (int i = m_childrenCacheObject.Count; i < TypeTreeNode.Children.Count; ++i)
            {
                var child = TypeTreeNode.Children[i];

                nodeReader = new RandomAccessReader(m_SerializedFile, child, m_Reader, offset, child.IsManagedReferenceRegistry || IsInManagedReferenceRegistry);
                m_childrenCacheObject.Add(child.Name, nodeReader);
                m_LastCachedChild = nodeReader;

                if (name == child.Name)
                    return nodeReader;

                offset += nodeReader.Size;
            }

            throw new KeyNotFoundException();
        }

        public int GetArraySize()
        {
            if (m_childrenCacheArray == null)
            {
                if (!IsArrayOfObjects)
                {
                    if (TypeTreeNode.IsArray)
                    {
                        return m_Reader.ReadInt32(Offset);
                    }

                    throw new InvalidOperationException("Node is not an array");
                }

                var arraySize = m_Reader.ReadInt32(Offset);
                m_childrenCacheArray = new List<RandomAccessReader>(arraySize);
            }

            return m_childrenCacheArray.Capacity;
        }

        RandomAccessReader GetArrayElement(int index)
        {
            RandomAccessReader nodeReader = null;
            var arraySize = GetArraySize();

            if (index < m_childrenCacheArray.Count)
                return m_childrenCacheArray[index];

            long offset;
            if (m_LastCachedChild == null)
            {
                offset = Offset + 4; // 4 is the array size.
            }
            else
            {
                offset = m_LastCachedChild.Offset + m_LastCachedChild.Size;
            }

            var dataNode = TypeTreeNode.Children[1];

            for (int i = m_childrenCacheArray.Count; i < arraySize; ++i)
            {
                nodeReader = new RandomAccessReader(m_SerializedFile, dataNode, m_Reader, offset, IsInManagedReferenceRegistry);
                m_childrenCacheArray.Add(nodeReader);
                m_LastCachedChild = nodeReader;

                if (index == i)
                    return nodeReader;

                offset += nodeReader.Size;
            }

            throw new IndexOutOfRangeException();
        }

        public int Count => IsArrayOfObjects ? GetArraySize() : (IsObject ? TypeTreeNode.Children.Count : 0);

        public RandomAccessReader this[string name] => GetChild(name);

        public RandomAccessReader this[int index] => GetArrayElement(index);

        public T GetValue<T>()
        {
            if (m_Value == null)
            {
                switch (Type.GetTypeCode(TypeTreeNode.CSharpType))
                {
                    case TypeCode.Int32:
                        m_Value = m_Reader.ReadInt32(Offset);
                        break;

                    case TypeCode.UInt32:
                        m_Value = m_Reader.ReadUInt32(Offset);
                        break;

                    case TypeCode.Single:
                        m_Value = m_Reader.ReadFloat(Offset);
                        break;

                    case TypeCode.Double:
                        m_Value = m_Reader.ReadDouble(Offset);
                        break;

                    case TypeCode.Int16:
                        m_Value = m_Reader.ReadInt16(Offset);
                        break;

                    case TypeCode.UInt16:
                        m_Value = m_Reader.ReadUInt16(Offset);
                        break;

                    case TypeCode.Int64:
                        m_Value = m_Reader.ReadInt64(Offset);
                        break;

                    case TypeCode.UInt64:
                        m_Value = m_Reader.ReadUInt64(Offset);
                        break;

                    case TypeCode.SByte:
                        m_Value = m_Reader.ReadUInt8(Offset);
                        break;

                    case TypeCode.Byte:
                    case TypeCode.Char:
                        m_Value = m_Reader.ReadUInt8(Offset);
                        break;

                    case TypeCode.Boolean:
                        m_Value = (m_Reader.ReadUInt8(Offset) != 0);
                        break;

                    case TypeCode.String:
                        var stringSize = m_Reader.ReadInt32(Offset);
                        m_Value = m_Reader.ReadString(Offset + 4, stringSize);
                        break;

                    default:
                        if (typeof(T).IsArray)
                        {
                            m_Value = ReadBasicTypeArray();
                            return (T)m_Value;
                        }

                        throw new Exception($"Can't get value of {TypeTreeNode.Type} type");
                }
            }

            return (T)Convert.ChangeType(m_Value, typeof(T));
        }

        Array ReadBasicTypeArray()
        {
            var arraySize = m_Reader.ReadInt32(Offset);
            var elementNode = TypeTreeNode.Children[1];

            // Special case for boolean arrays.
            if (elementNode.CSharpType == typeof(bool))
            {
                var tmpArray = new byte[arraySize];
                var boolArray = new bool[arraySize];

                m_Reader.ReadArray(Offset + 4, arraySize * elementNode.Size, tmpArray);

                for (int i = 0; i < arraySize; ++i)
                {
                    boolArray[i] = tmpArray[i] != 0;
                }

                return boolArray;
            }
            else
            {
                var array = Array.CreateInstance(elementNode.CSharpType, arraySize);

                m_Reader.ReadArray(Offset + 4, arraySize * elementNode.Size, array);

                return array;
            }
        }

        class Enumerator : IEnumerator<RandomAccessReader>
        {
            int m_Index = -1;
            RandomAccessReader m_NodeReader;

            public Enumerator(RandomAccessReader nodeReader)
            {
                m_NodeReader = nodeReader;
            }

            public RandomAccessReader Current
            {
                get
                {
                    if (m_NodeReader.IsObject)
                    {
                        return m_NodeReader.GetChild(m_NodeReader.TypeTreeNode.Children[m_Index].Name);
                    }
                    else if (m_NodeReader.IsArrayOfObjects)
                    {
                        return m_NodeReader.GetArrayElement(m_Index);
                    }

                    throw new InvalidOperationException();
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                ++m_Index;
                return m_Index < m_NodeReader.Count;
            }

            public void Reset()
            {
                m_Index = -1;
            }
        }

        public IEnumerator<RandomAccessReader> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }
    }
}
