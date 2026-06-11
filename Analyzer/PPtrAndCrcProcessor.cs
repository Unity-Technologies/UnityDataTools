using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Force.Crc32;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer;

// Walks the TypeTree of a serialized object to do two things in a single pass:
//  1. Extract every PPtr (object reference). A callback is executed for each one, receiving the
//     property path that leads to it (e.g. "m_MyObject.m_MyArray[2].m_PPtrProperty").
//  2. Accumulate a CRC32 over the object's serialized bytes, including the content of external
//     streams (texture/mesh/audio data stored in companion .resS/.resource files). This CRC is a
//     content fingerprint used to detect whether two objects are identical.
// CRC computation can be disabled (skipCrc) while still extracting references.
public class PPtrAndCrcProcessor : IDisposable
{
    public delegate int CallbackDelegate(long objectId, int fileId, long pathId, string propertyPath, string propertyType);

    // Content-addressed stream paths (new ContentDirectory build output) look like
    // "cah:/<hash>". The hash already identifies the content, so the path itself is
    // folded into the CRC instead of opening the (differently named) resource file.
    // Matched case-insensitively since the scheme casing is not guaranteed.
    private const string ContentAddressedPrefix = "cah:/";

    // Configuration shared across all objects, set once in the constructor.
    private SerializedFile m_SerializedFile;    // file being analyzed; used to resolve referenced managed type trees
    private UnityFileReader m_Reader;            // reader over the serialized file holding the object data
    private string m_Folder;                     // directory of the serialized file; used to find companion resource files
    private bool m_SkipCrc;                      // when true, skip CRC computation (references are still extracted)
    private CallbackDelegate m_Callback;         // invoked for each PPtr; returns the referenced object's id

    // Readers for external resource (.resS/.resource) files, opened on demand, reused across
    // objects, and disposed in Dispose().
    private Dictionary<string, UnityFileReader> m_resourceReaders = new();

    // Reusable scratch buffers, kept as fields to avoid allocating per object/property.
    private StringBuilder m_StringBuilder = new();    // builds the current property path during the walk
    private byte[] m_pptrBytes = new byte[4];         // holds a referenced object id while feeding it to the CRC

    // State for the object currently being processed, (re)initialized by each Process() call.
    private long m_Offset;       // current read position within m_Reader
    private long m_ObjectId;     // analyzer id of the object being processed, passed to the callback
    private uint m_Crc32;        // CRC accumulated so far for this object

    // serializedFile: the file whose objects are analyzed (used to resolve referenced managed types).
    // reader:         reader over that file's bytes; Process() walks each object through it.
    // folder:         directory containing the serialized file; companion .resS/.resource files are
    //                 looked up here when a non-content-addressed external stream contributes to the CRC.
    // skipCrc:        when true, the tree is still walked to emit references but no CRC is computed.
    // callback:       called for every PPtr found; its return value (the referenced object's id) is
    //                 folded into the CRC.
    public PPtrAndCrcProcessor(SerializedFile serializedFile, UnityFileReader reader, string folder,
        bool skipCrc, CallbackDelegate callback)
    {
        m_SerializedFile = serializedFile;
        m_Reader = reader;
        m_Folder = folder;
        m_SkipCrc = skipCrc;
        m_Callback = callback;
    }

    public void Dispose()
    {
        foreach (var r in m_resourceReaders.Values)
        {
            r?.Dispose();
        }

        m_resourceReaders.Clear();
    }

    // Walks the serialized object rooted at `node`, whose data starts at `offset` in the reader,
    // emitting every PPtr through the callback. Returns a CRC32 fingerprint of the object's content
    // (0 when CRC is disabled). `objectId` is the analyzer id of this object, forwarded to the callback.
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
            AppendCrc(m_Offset, node.Size);
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

            var offset = node.Children[0].Size == 4 ? m_Reader.ReadInt32(m_Offset) : m_Reader.ReadInt64(m_Offset);
            m_Offset += node.Children[0].Size;

            var size = m_Reader.ReadInt32(m_Offset);
            m_Offset += 4;

            var stringSize = m_Reader.ReadInt32(m_Offset);
            var filename = m_Reader.ReadString(m_Offset + 4, stringSize);
            m_Offset += stringSize + 4;
            m_Offset = (m_Offset + 3) & ~(3);

            if (size > 0)
            {
                AppendStreamCrc(offset, size, filename);
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
                AppendStreamCrc(offset, size, filename);
            }
        }
        else if (node.CSharpType == typeof(string))
        {
            var prevOffset = m_Offset;
            m_Offset += m_Reader.ReadInt32(m_Offset) + 4;
            AppendCrc(prevOffset, (int)(m_Offset - prevOffset));
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
            AppendCrc(m_Offset, dataNode.Size * arraySize + 4);
            m_Offset += dataNode.Size * arraySize + 4;
        }
        else
        {
            AppendCrc(m_Offset, 4);
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
                    AppendCrc(m_Offset, 8);
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
        AppendCrc(m_Offset, node.Children[0].Size);
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
        AppendCrc(m_Offset, stringSize + 4);
        var className = m_Reader.ReadString(m_Offset + 4, stringSize);
        m_Offset += stringSize + 4;
        m_Offset = (m_Offset + 3) & ~(3);

        stringSize = m_Reader.ReadInt32(m_Offset);
        AppendCrc(m_Offset, stringSize + 4);
        var namespaceName = m_Reader.ReadString(m_Offset + 4, stringSize);
        m_Offset += stringSize + 4;
        m_Offset = (m_Offset + 3) & ~(3);

        stringSize = m_Reader.ReadInt32(m_Offset);
        AppendCrc(m_Offset, stringSize + 4);
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

            if (!m_SkipCrc)
            {
                m_pptrBytes[0] = (byte)(refId >> 24);
                m_pptrBytes[1] = (byte)(refId >> 16);
                m_pptrBytes[2] = (byte)(refId >> 8);
                m_pptrBytes[3] = (byte)(refId);
                m_Crc32 = Crc32Algorithm.Append(m_Crc32, m_pptrBytes);
            }
        }
    }

    // Extends the CRC with a range of the main serialized file, unless CRC is disabled.
    private void AppendCrc(long offset, int size)
    {
        if (!m_SkipCrc)
            m_Crc32 = m_Reader.ComputeCRC(offset, size, m_Crc32);
    }

    // Extends the CRC with the content of an external stream segment (StreamingInfo /
    // StreamedResource), unless CRC is disabled. Content-addressed paths fold in the path
    // string; other paths read the actual bytes from the companion resource file.
    private void AppendStreamCrc(long offset, int size, string path)
    {
        if (m_SkipCrc)
            return;

        // A cah:/ stream always references the entire resource file: the hash in the path
        // is the hash of the whole file, so the path uniquely identifies the bytes and we
        // fold it into the CRC rather than reading them. The offset/size fields only exist
        // for backward compatibility with the older output format that packed multiple
        // resources into one file; ContentDirectory builds never do this (offset is 0 and
        // size is the full file), which is why ignoring offset/size here is correct.
        if (path.StartsWith(ContentAddressedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            m_Crc32 = Crc32Algorithm.Append(m_Crc32, Encoding.UTF8.GetBytes(path));
            return;
        }

        var resourceFile = GetResourceReader(path);
        if (resourceFile != null)
            m_Crc32 = resourceFile.ComputeCRC(offset, size, m_Crc32);
    }

    private UnityFileReader GetResourceReader(string filename)
    {
        var slashPos = filename.LastIndexOf('/');
        if (slashPos > 0)
        {
            filename = filename.Remove(0, slashPos + 1);
        }

        if (!m_resourceReaders.TryGetValue(filename, out var reader))
        {
            try
            {
                reader = new UnityFileReader("archive:/" + filename, 4 * 1024 * 1024);
            }
            catch (Exception)
            {
                try
                {
                    reader = new UnityFileReader(Path.Join(m_Folder, filename), 4 * 1024 * 1024);
                }
                catch (Exception)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"Error opening resource file {filename}");
                    reader = null;
                }
            }

            m_resourceReaders[filename] = reader;
        }

        return reader;
    }

}
