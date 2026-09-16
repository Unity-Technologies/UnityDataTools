using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace UnityDataTools.FileSystem;

// One [SerializeReference] instance owned by a serialized object. The fields that point at it store
// only its Rid, so a shared instance appears once here and is referred to from several places.
public sealed class ManagedReferenceEntry
{
    public long Rid { get; init; }

    // A null reference: no type, no data. Only the registry frame format can express one.
    public bool IsNull { get; init; }

    // The instance's concrete type, which is what lays out its data. Empty when IsNull.
    public string ClassName { get; init; } = "";
    public string Namespace { get; init; } = "";
    public string AssemblyName { get; init; } = "";

    // Absolute position and size of the instance's data in the file. Zero when IsNull.
    public long DataOffset { get; init; }
    public long DataSize { get; init; }
}

// The [SerializeReference] instances owned by one serialized object.
//
// From SerializedFile version 25 the registry is a self-delimiting frame in the object's data,
// sitting immediately before the data of the field flagged HasSerializedRefs. No TypeTree node
// describes it, so it is read through the native parser rather than reimplemented here - it is the
// first piece of object layout a TypeTree cannot express, and a second implementation of a layout
// documented only in the engine source would drift from it.
//
// Earlier files describe the registry with TypeTree nodes instead (versions 1 and 2 of the registry
// itself), which the callers read through the node walk and present as the same entries.
public sealed class ManagedReferenceRegistry
{
    // The only frame layout the native parser accepts. Unrelated to the serialized file version.
    public const int FrameVersion = 3;

    // Frame header: [int32 version][uint32 byteLength], where byteLength counts to the frame's end.
    const int FrameHeaderSize = 8;

    public int Version { get; private init; }

    // Bytes the frame occupies, counted from its first byte. Add to the frame's position to reach
    // the data of the field it precedes.
    public long FrameSize { get; private init; }

    // Absolute position of the first data blob, i.e. one past the frame's header and tables.
    public long BlobsOffset { get; private init; }

    public IReadOnlyList<ManagedReferenceEntry> Entries { get; private init; }

    // Builds a registry from entries a caller read some other way: versions 1 and 2 are described by
    // TypeTree nodes, so the node walk produces the entries and they are presented the same way.
    internal static ManagedReferenceRegistry FromEntries(int version, IReadOnlyList<ManagedReferenceEntry> entries)
    {
        return new ManagedReferenceRegistry { Version = version, Entries = entries };
    }

    // Reads the frame at `offset`. `availableBytes` is what the caller actually holds from there,
    // normally to the end of the object - the frame's own length field is untrusted and must never
    // be used as the bound. Returns null when there is no readable frame, in which case the
    // position is ordinary field data.
    public static ManagedReferenceRegistry ReadFrame(UnityFileReader reader, long offset, long availableBytes)
    {
        if (availableBytes < FrameHeaderSize || reader.ReadInt32(offset) != FrameVersion)
            return null;

        var frameSize = FrameHeaderSize + (long)reader.ReadUInt32(offset + 4);
        if (frameSize > availableBytes)
            return null;

        var bytes = new byte[frameSize];
        reader.ReadRange(offset, (int)frameSize, bytes);

        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var data = pinned.AddrOfPinnedObject();
            var size = (ulong)frameSize;

            var r = DllWrapper.GetRegistryFrameInfo(data, size, out var info);
            if (r == ReturnCode.FileFormatError)
                return null;
            UnityFileSystem.HandleErrors(r);

            var types = new RegistryFrameType[info.TypeCount];
            if (info.TypeCount > 0)
                UnityFileSystem.HandleErrors(DllWrapper.GetRegistryFrameTypes(data, size, types, types.Length));

            var records = new RegistryFrameRecord[info.RecordCount];
            if (info.RecordCount > 0)
                UnityFileSystem.HandleErrors(DllWrapper.GetRegistryFrameRecords(data, size, records, records.Length));

            var entries = new List<ManagedReferenceEntry>(records.Length);
            foreach (var record in records)
            {
                if (record.TypeIndex < 0)
                {
                    entries.Add(new ManagedReferenceEntry { Rid = record.Rid, IsNull = true });
                    continue;
                }

                var type = types[record.TypeIndex];
                entries.Add(new ManagedReferenceEntry
                {
                    Rid = record.Rid,
                    ClassName = type.ClassName,
                    Namespace = type.NamespaceName,
                    AssemblyName = type.AssemblyName,
                    // Frame offsets are relative to its first byte.
                    DataOffset = offset + (long)record.BlobOffset,
                    DataSize = record.ByteSize,
                });
            }

            return new ManagedReferenceRegistry
            {
                Version = info.Version,
                FrameSize = (long)info.FrameSize,
                BlobsOffset = offset + (long)info.BlobsOffset,
                Entries = entries,
            };
        }
        finally
        {
            pinned.Free();
        }
    }
}
