using System;
using System.Collections.Generic;
using System.Text;

namespace UnityDataTools.FileSystem;

// An archive node is a file in an archive.
public struct ArchiveNode
{
    public string Path;
    public long Size;
    public ArchiveNodeFlags Flags;
}

// Class used to open a Unity archive file (such as an AssetBundle).
public class UnityArchive : IDisposable
{
    internal UnityArchiveHandle m_Handle;
    Lazy<List<ArchiveNode>> m_Nodes;
    CompressionType m_CompressionType;

    public IReadOnlyList<ArchiveNode> Nodes => m_Nodes.Value.AsReadOnly();
    public CompressionType CompressionType => m_CompressionType;

    internal UnityArchive(UnityArchiveHandle handle)
    {
        m_Handle = handle;
        m_Nodes = new Lazy<List<ArchiveNode>>(() => GetArchiveNodes());
        var r = DllWrapper.GetArchiveCompression(m_Handle, out m_CompressionType);
        UnityFileSystem.HandleErrors(r);
    }

    List<ArchiveNode> GetArchiveNodes()
    {
        var r = DllWrapper.GetArchiveNodeCount(m_Handle, out var count);
        UnityFileSystem.HandleErrors(r);

        if (count == 0)
            return null;

        var nodes = new List<ArchiveNode>(count);
        var path = new StringBuilder(512);

        for (var i = 0; i < count; ++i)
        {
            r = DllWrapper.GetArchiveNode(m_Handle, i, path, path.Capacity, out var size, out var flags);
            UnityFileSystem.HandleErrors(r);

            nodes.Add(new ArchiveNode() { Path = path.ToString(), Size = size, Flags = flags });
        }

        return nodes;
    }

    public void Dispose()
    {
        if (m_Handle != null && !m_Handle.IsInvalid)
        {
            m_Handle.Dispose();
            m_Nodes = new Lazy<List<ArchiveNode>>(() => GetArchiveNodes());
        }
    }
}
