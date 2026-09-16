using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UnityDataTools.FileSystem;

public class UnityArchiveHandle : SafeHandle
{
    public UnityArchiveHandle() : base(IntPtr.Zero, true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        return DllWrapper.UnmountArchive(handle) == ReturnCode.Success;
    }
}

public class UnityFileHandle : SafeHandle
{
    public UnityFileHandle() : base(IntPtr.Zero, true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        return DllWrapper.CloseFile(handle) == ReturnCode.Success;
    }
}

public class SerializedFileHandle : SafeHandle
{
    public SerializedFileHandle() : base(IntPtr.Zero, true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        return DllWrapper.CloseSerializedFile(handle) == ReturnCode.Success;
    }
}

public class TypeTreeHandle : SafeHandle
{
    public TypeTreeHandle() : base(IntPtr.Zero, true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        return true;
    }

    internal IntPtr Handle => handle;
}

public enum ReturnCode
{
    Success,
    AlreadyInitialized,
    NotInitialized,
    FileNotFound,
    FileFormatError,
    InvalidArgument,
    HigherSerializedFileVersion,
    DestinationBufferTooSmall,
    InvalidObjectId,
    UnknownError,
    FileError,
    ErrorCreatingArchiveFile,
    ErrorAddingFileToArchive,
    TypeNotFound,
    HigherTypeTreeVersion,
    RequiresSubtreeApi,
}

[Flags]
public enum ArchiveNodeFlags
{
    None = 0,
    Directory = 1 << 0,
    Deleted = 1 << 1,
    SerializedFile = 1 << 2,
}

public enum CompressionType
{
    None,
    Lzma,
    Lz4,
    Lz4HC,
};

public enum SeekOrigin
{
    Begin,
    Current,
    End,
}

public enum ExternalReferenceType
{
    NonAssetType,
    DeprecatedCachedAssetType,
    SerializedAssetType,
    MetaAssetType,
}

[StructLayout(LayoutKind.Sequential)]
public struct ObjectInfo
{
    public readonly long Id;
    public readonly long Offset;
    public readonly long Size;
    public readonly int TypeId;

    public ObjectInfo(long id, long offset, long size, int typeId)
    {
        Id = id;
        Offset = offset;
        Size = size;
        TypeId = typeId;
    }
}
[Flags]
public enum TypeTreeFlags
{
    None = 0,
    IsArray = 1 << 0,
    IsManagedReference = 1 << 1,
    IsManagedReferenceRegistry = 1 << 2,
    IsArrayOfRefs = 1 << 3,
    // The registry frame precedes this node's data when its type tree is used as a root.
    HasSerializedRefs = 1 << 4,
    IsSharedSubtreeRef = 1 << 5,
}

[Flags]
public enum TypeTreeMetaFlags
{
    None = 0,
    AlignBytes = 1 << 14,
    AnyChildUsesAlignBytes = 1 << 15,
}

public enum TypeTreeCategory
{
    ObjectType = 0,
    RefType = 1,
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct TypeTreeInfo
{
    public readonly int TypeId;
    public readonly int SerializedSize;
    public readonly TypeTreeCategory Category;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public readonly uint[] Hash;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public readonly string ClassName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public readonly string NamespaceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public readonly string AssemblyName;
}

// Parsed header of a [SerializeReference] registry frame. Every offset is relative to the frame's
// first byte, which is the byte passed to the GetRegistryFrame* calls as the frame data.
[StructLayout(LayoutKind.Sequential)]
public struct RegistryFrameInfo
{
    public int Version;
    public int TypeCount;
    public int RecordCount;
    public ulong FrameSize;
    public ulong BlobsOffset;
}

// One entry of the frame's Types table: the fully qualified name of a concrete [SerializeReference]
// type, in the form GetRefTypeTypeTree matches against.
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct RegistryFrameType
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string ClassName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string NamespaceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string AssemblyName;
}

[StructLayout(LayoutKind.Sequential)]
public struct RegistryFrameRecord
{
    public long Rid;
    public int TypeIndex;   // index into the Types table, or -1 for a null entry, which carries no blob
    public uint ByteSize;
    public ulong BlobOffset;
}

public static class DllWrapper
{
    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_Init")]
    public static extern ReturnCode Init();

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_Cleanup")]
    public static extern ReturnCode Cleanup();

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_MountArchive")]
    public static extern ReturnCode MountArchive([MarshalAs(UnmanagedType.LPUTF8Str)] string path, [MarshalAs(UnmanagedType.LPUTF8Str)] string mountPoint, out UnityArchiveHandle handle);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_UnmountArchive")]
    public static extern ReturnCode UnmountArchive(IntPtr handle);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetArchiveNodeCount")]
    public static extern ReturnCode GetArchiveNodeCount(UnityArchiveHandle handle, out int count);

    // Note: Strings returned from native (here and in the other StringBuilder-based calls) come back as
    // UTF-8 bytes but are marshalled as the system code page which could be lossy, especially on Windows.
    // For this API these are internal archive/serialized-file names which we expect to be ASCII, so it's not an issue in practice.
    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetArchiveNode")]
    public static extern ReturnCode GetArchiveNode(UnityArchiveHandle handle, int nodeIndex, StringBuilder path, int pathLen, out long size, out ArchiveNodeFlags flags);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_CreateArchive")]
    private static extern ReturnCode CreateArchiveNative(IntPtr[] sourceFiles, IntPtr[] aliases, bool[] isSerializedFile, int count,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string archiveFile, CompressionType compression, out int crc);

    // The native library expects UTF-8 paths. LPUTF8Str handles the scalar strings, but it
    // can't be used as an array subtype, so the string arrays are marshalled to UTF-8 by hand.
    public static ReturnCode CreateArchive(string[] sourceFiles, string[] aliases, bool[] isSerializedFile, int count,
        string archiveFile, CompressionType compression, out int crc)
    {
        if (count < 0 || count > sourceFiles.Length || count > aliases.Length || count > isSerializedFile.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        // Marshal only the first `count` entries, since that's all the native call consumes.
        var sourcePtrs = new IntPtr[count];
        var aliasPtrs = new IntPtr[count];
        try
        {
            for (int i = 0; i < count; ++i)
            {
                sourcePtrs[i] = Marshal.StringToCoTaskMemUTF8(sourceFiles[i]);
                aliasPtrs[i] = Marshal.StringToCoTaskMemUTF8(aliases[i]);
            }

            return CreateArchiveNative(sourcePtrs, aliasPtrs, isSerializedFile, count, archiveFile, compression, out crc);
        }
        finally
        {
            foreach (var p in sourcePtrs)
                Marshal.FreeCoTaskMem(p);
            foreach (var p in aliasPtrs)
                Marshal.FreeCoTaskMem(p);
        }
    }

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_OpenFile")]
    public static extern ReturnCode OpenFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out UnityFileHandle handle);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl, EntryPoint = "UFS_ReadFile")]
    public static extern ReturnCode ReadFile(UnityFileHandle handle, long size,
        [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, out long actualSize);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_SeekFile")]
    public static extern ReturnCode SeekFile(UnityFileHandle handle, long offset, SeekOrigin origin, out long newPosition);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetFileSize")]
    public static extern ReturnCode GetFileSize(UnityFileHandle handle, out long size);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_CloseFile")]
    public static extern ReturnCode CloseFile(IntPtr handle);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_OpenSerializedFile")]
    public static extern ReturnCode OpenSerializedFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out SerializedFileHandle handle);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_CloseSerializedFile")]
    public static extern ReturnCode CloseSerializedFile(IntPtr handle);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetExternalReferenceCount")]
    public static extern ReturnCode GetExternalReferenceCount(SerializedFileHandle handle, out int count);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetExternalReference")]
    public static extern ReturnCode GetExternalReference(SerializedFileHandle handle, int index, StringBuilder path, int pathLen, StringBuilder guid, out ExternalReferenceType type);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetObjectCount")]
    public static extern ReturnCode GetObjectCount(SerializedFileHandle handle, out int count);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetObjectInfo")]
    public static extern ReturnCode GetObjectInfo(SerializedFileHandle handle, [In, Out] ObjectInfo[] objectData, int len);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetTypeTree")]
    public static extern ReturnCode GetTypeTree(SerializedFileHandle handle, long objectId, out TypeTreeHandle typeTree);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetRefTypeTypeTree")]
    public static extern ReturnCode GetRefTypeTypeTree(SerializedFileHandle handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string className,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string namespaceName, [MarshalAs(UnmanagedType.LPUTF8Str)] string assemblyName, out TypeTreeHandle typeTree);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_AddTypeTreeSourceFromFile")]
    public static extern ReturnCode AddTypeTreeSourceFromFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out long handle);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetTypeTreeNodeInfo")]
    public static extern ReturnCode GetTypeTreeNodeInfo(TypeTreeHandle handle, int node, StringBuilder type, int typeLen,
        StringBuilder name, int nameLen, out int offset, out int size, [MarshalAs(UnmanagedType.U4)] out TypeTreeFlags flags,
        [MarshalAs(UnmanagedType.U4)] out TypeTreeMetaFlags metaFlags, out int firstChildNode,
        out int nextNode);

    // As GetTypeTreeNodeInfo, but for a node inside a shared subtree. A null subtree means the type
    // tree's own nodes, so this call alone covers both sides. offset is unset inside a subtree.
    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetTypeTreeSubtreeNodeInfo")]
    public static extern ReturnCode GetTypeTreeSubtreeNodeInfo(TypeTreeHandle handle, IntPtr subtree, int node,
        StringBuilder type, int typeLen, StringBuilder name, int nameLen, out int offset, out int size,
        [MarshalAs(UnmanagedType.U4)] out TypeTreeFlags flags, [MarshalAs(UnmanagedType.U4)] out TypeTreeMetaFlags metaFlags,
        out int firstChildNode, out int nextNode);

    // Resolves an IsSharedSubtreeRef node. The reference stands in for the subtree's root, so
    // firstChildNode is that root's first child; 0 means the compound has no fields.
    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetTypeTreeRefSubtree")]
    public static extern ReturnCode GetTypeTreeRefSubtree(TypeTreeHandle handle, IntPtr subtree, int node,
        out IntPtr refSubtree, out int firstChildNode);

    // The registry frame is object data rather than type information, so these take the frame's bytes
    // and a bound on them. The frame's own length field is untrusted, so size must be what the caller
    // actually holds.
    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetRegistryFrameInfo")]
    public static extern ReturnCode GetRegistryFrameInfo(IntPtr data, ulong size, out RegistryFrameInfo info);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetRegistryFrameTypes")]
    public static extern ReturnCode GetRegistryFrameTypes(IntPtr data, ulong size,
        [Out] RegistryFrameType[] types, int len);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetRegistryFrameRecords")]
    public static extern ReturnCode GetRegistryFrameRecords(IntPtr data, ulong size,
        [Out] RegistryFrameRecord[] records, int len);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetDllVersion")]
    public static extern ReturnCode GetDllVersion(out int version);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetUnityVersion")]
    public static extern ReturnCode GetUnityVersion(StringBuilder version, int versionLen);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetSerializedFileVersion")]
    public static extern ReturnCode GetSerializedFileVersion(SerializedFileHandle handle, out int version);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetTypeTreeCount")]
    public static extern ReturnCode GetTypeTreeCount(SerializedFileHandle handle, out int count);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetTypeTreeInfo")]
    public static extern ReturnCode GetTypeTreeInfo(SerializedFileHandle handle, int index, out TypeTreeInfo info);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_GetTypeTreeByIndex")]
    public static extern ReturnCode GetTypeTreeByIndex(SerializedFileHandle handle, int index, out TypeTreeHandle typeTree);

    [DllImport("UnityFileSystemApi",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "UFS_RemoveTypeTreeSource")]
    public static extern ReturnCode RemoveTypeTreeSource(long handle);
}
