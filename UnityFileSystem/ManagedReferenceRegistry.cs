using System;
using System.Collections.Generic;
using System.IO;
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

    // Absolute position of the instance's data in the file. Zero when IsNull.
    public long DataOffset { get; init; }
}

// The [SerializeReference] instances owned by one serialized object.
//
// From SerializedFile version 25 the registry is a self-delimiting frame leading the C# class's own
// data - after the built-in fields, before the first field the script declares, which is the one
// flagged HasSerializedRefs. It is the first piece of object layout no TypeTree node describes, so
// the tables inside it cannot be walked the way every other field is.
//
// They are not parsed here either: UFS_GetRegistryFrame* wraps the engine's own frame parser, so
// this class calls into the one implementation that exists rather than becoming a second one. Only
// the 8-byte header is read directly, in GetFrameSize, which is all a walker needs to step over a
// frame it does not want to read.
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

    // Bytes the frame at `offset` occupies, read from its header alone. This is how a walker that
    // only needs to step over the frame avoids parsing its tables.
    //
    // `availableBytes` is what the caller actually holds from `offset`, normally to the end of the
    // object. The frame's own length field is untrusted and must never be used as the bound.
    public static long GetFrameSize(UnityFileReader reader, long offset, long availableBytes)
    {
        if (availableBytes < FrameHeaderSize)
            throw new InvalidDataException($"No room for a [SerializeReference] registry frame at offset {offset}");

        var version = reader.ReadInt32(offset);
        if (version != FrameVersion)
            throw new InvalidDataException($"Unsupported [SerializeReference] registry version {version} at offset {offset}");

        var frameSize = FrameHeaderSize + (long)reader.ReadUInt32(offset + 4);
        if (frameSize > availableBytes)
            throw new InvalidDataException($"[SerializeReference] registry frame at offset {offset} runs past the end of the object");

        return frameSize;
    }

    // Reads the frame at `offset`, including its tables. `availableBytes` is bounded as it is for
    // GetFrameSize. Throws when there is no readable frame, which callers reach only where a node
    // flagged HasSerializedRefs says one is present.
    public static ManagedReferenceRegistry ReadFrame(UnityFileReader reader, long offset, long availableBytes)
    {
        var frameSize = GetFrameSize(reader, offset, availableBytes);
        var bytes = new byte[frameSize];
        reader.ReadRange(offset, (int)frameSize, bytes);

        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var data = pinned.AddrOfPinnedObject();
            var size = (ulong)frameSize;

            var r = DllWrapper.GetRegistryFrameInfo(data, size, out var info);
            if (r == ReturnCode.FileFormatError)
                throw new InvalidDataException($"Malformed [SerializeReference] registry frame at offset {offset}");
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
