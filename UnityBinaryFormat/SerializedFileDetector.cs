using System;
using System.IO;
using ExternalReference = UnityDataTools.FileSystem.ExternalReference;
using ExternalReferenceType = UnityDataTools.FileSystem.ExternalReferenceType;
using ObjectInfo = UnityDataTools.FileSystem.ObjectInfo;

namespace UnityDataTools.BinaryFormat;

/// <summary>
/// Information extracted from a Unity SerializedFile header.
/// </summary>
public class SerializedFileInfo
{
    public uint Version { get; set; }
    public ulong FileSize { get; set; }
    public ulong MetadataSize { get; set; }
    public ulong DataOffset { get; set; }
    public byte Endianness { get; set; }
    public bool IsLegacyFormat { get; set; }
}

/// <summary>
/// Summary information about a single TypeTree entry within a SerializedFile metadata section.
/// Does not contain the full TypeTree node graph — only the per-entry header fields.
///
/// Each entry corresponds to one element of either the regular type list (m_Types) or the
/// SerializeReference type list (m_RefTypes, version >= 20). Fields that are not applicable
/// for a given entry use well-defined sentinel values:
///   - UnityHash128 fields use IsZero == true to indicate "not present"
///   - short ScriptTypeIndex uses -1 to indicate "not a script type"
///   - string fields (ClassName, Namespace, AssemblyName) use string.Empty for regular type entries
///   - TypeDependencies uses an empty array for ref type entries or version < 21
/// </summary>
public class TypeTreeInfo
{
    // -----------------------------------------------------------------------
    // Fields present for all versions >= 16 (kRefactoredClassId)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Unity ClassID for this type (e.g. 114 = MonoBehaviour, 115 = MonoScript).
    /// Corresponds to m_PersistentTypeID in the file. For ref type entries this is -1
    /// and the type is identified by the ClassName/Namespace/AssemblyName triple instead.
    /// </summary>
    public int PersistentTypeID { get; set; }

    /// <summary>
    /// True for types that represent Prefab-stripped objects. In text/YAML files this corresponds to the stripped keyword:
    /// e.g. --- !u!123 &111 stripped.
    /// </summary>
    /// <remarks>This field is not related to the presence of a TypeTree blob in the file.</remarks>
    public bool IsStrippedType { get; set; }

    /// <summary>
    /// Index into the file's script type list (m_ScriptTypes).
    /// -1 (sentinel) means this entry is not backed by a MonoScript (i.e. a native Unity type).
    /// </summary>
    public short ScriptTypeIndex { get; set; } = -1;

    // -----------------------------------------------------------------------
    // Hash fields (version >= 13, kHasTypeTreeHashes)
    // -----------------------------------------------------------------------

    /// <summary>
    /// MD4 hash of (assembly name + namespace + class name) identifying the script.
    /// Written for MonoBehaviour (ClassID 114), unknown script types, and entries where
    /// ScriptTypeIndex >= 0. IsZero == true indicates this field is not applicable for
    /// this entry (native type with no associated MonoScript).
    /// </summary>
    public UnityHash128 ScriptID { get; set; }

    /// <summary>
    /// Hash of the TypeTree structure (field names, types, byte sizes, alignment flags),
    /// computed via MD4 over the node graph. Used to detect type schema changes between
    /// the version stored in the file and the current runtime type, and to deduplicate
    /// type entries when writing serialized files.  Sometimes referred to as the "OldTypeHash"
    /// because it refers to the type at the time it was serialized, which might be older than
    /// the current type.
    /// Used for compatibility checking at load time.
    /// </summary>
    public UnityHash128 TypeTreeStructureHash { get; set; }

    // -----------------------------------------------------------------------
    // TypeTree inline/extracted data (only when EnableTypeTree = true)
    // -----------------------------------------------------------------------

    /// <summary>
    /// XXH3 content hash of the TypeTree blob, e.g. hash of the raw binary encoding
    /// of the TypeTree definition. Stored explicitly in the metadata for
    /// version >= 23 (kExtractedTypeTreeSupport). IsZero == true indicates this field
    /// was not present in the metadata (version < 23 or no inline TypeTree).
    /// This is used for TypeTree deduplication and caching.
    /// </summary>
    public UnityHash128 TypeTreeContentHash { get; set; }

    /// <summary>
    /// Actual size in bytes of the TypeTree blob for this entry.
    /// 0 when InlineTypeTree is false (stripped, EnableTypeTree=false, or extracted to
    /// an external store in version >= 23). For version < 23 where the size is not
    /// stored explicitly, this is computed by skipping over the blob during parsing.
    /// </summary>
    public uint TypeTreeSerializedSize { get; set; }

    /// <summary>
    /// True when the TypeTree blob is present inline in this file's metadata and can be
    /// read without an external TypeTree store. False when EnableTypeTree is false, or
    /// TypeTreeSerializedSize is 0 (blob extracted to an external store, version >= 23).
    /// Note: IsStrippedType is orthogonal and does not affect TypeTree presence.
    /// </summary>
    public bool InlineTypeTree { get; set; }

    // -----------------------------------------------------------------------
    // Ref-type identification (only for entries in SerializedReferenceTypeTrees,
    // version >= 20)
    // -----------------------------------------------------------------------

    /// <summary>
    /// C# class name of the SerializeReference type.
    /// string.Empty for regular (non-ref) type entries.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// C# namespace of the SerializeReference type.
    /// string.Empty for regular (non-ref) type entries.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Assembly name of the SerializeReference type.
    /// string.Empty for regular (non-ref) type entries.
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    // -----------------------------------------------------------------------
    // Non-ref type dependency list (only for entries in TypeTrees,
    // version >= 21, kStoresTypeDependencies)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Indices into the SerializedReferenceTypeTrees array representing the
    /// SerializeReference types that objects of this type may reference.
    /// Empty array for ref type entries or files with version < 21.
    /// </summary>
    public int[] TypeDependencies { get; set; } = Array.Empty<int>();
}

/// <summary>
/// A reference to a MonoScript object that backs a C# MonoBehaviour type recorded in this file.
/// Corresponds to one entry in the file's m_ScriptTypes list.
/// </summary>
public class ScriptType
{
    /// <summary>
    /// Index into the file's external references list, identifying which SerializedFile contains
    /// the MonoScript object. 0 = this file itself; 1+ = 1-based index into the externals list.
    /// </summary>
    public int FileID { get; set; }

    /// <summary>
    /// The object ID (localIdentifierInFile) of the MonoScript within the identified file.
    /// </summary>
    public long PathID { get; set; }
}

/// <summary>
/// Information extracted from the beginning of a Unity SerializedFile metadata section.
/// </summary>
public class SerializedFileMetadata
{
    public string UnityVersion { get; set; }
    public uint TargetPlatform { get; set; }
    public bool EnableTypeTree { get; set; }

    /// <summary>
    /// Number of regular (object) TypeTree entries (m_Types).
    /// Populated even when TypeTrees is null.
    /// </summary>
    public int TypeTreeCount { get; set; }

    /// <summary>
    /// Number of SerializeReference TypeTree entries (m_RefTypes).
    /// Always 0 for files with version < 20 (kSupportsRefObject).
    /// </summary>
    public int SerializedReferenceTypeTreeCount { get; set; }

    /// <summary>
    /// Summary of each regular type entry. Null until the TypeTree section has been parsed.
    /// </summary>
    public TypeTreeInfo[] TypeTrees { get; set; }

    /// <summary>
    /// Summary of each SerializeReference type entry.
    /// Empty array for files with version < 20.
    /// </summary>
    public TypeTreeInfo[] SerializedReferenceTypeTrees { get; set; }

    /// <summary>
    /// List of MonoScript references for the C# types used in this file.
    /// Each entry points to the MonoScript object (in this file or an external file) that backs
    /// a C# MonoBehaviour-derived type whose ScriptTypeIndex is the index of that entry here.
    /// Null until the metadata section has been parsed.
    /// </summary>
    public ScriptType[] ScriptTypes { get; set; }

    /// <summary>
    /// List of all objects recorded in the file's object table.
    /// Null until the metadata section has been parsed.
    /// </summary>
    public ObjectInfo[] ObjectList { get; set; }

    /// <summary>
    /// List of external file references recorded in the file's externals table.
    /// Null until the metadata section has been parsed.
    /// </summary>
    public ExternalReference[] ExternalReferences { get; set; }
}

/// <summary>
/// Utility for detecting Unity SerializedFile format by reading and validating the file header.
///
/// Unity SerializedFiles have evolved through several format versions:
///
/// Version < 9:
///   - 20-byte header (SerializedFileHeader32) with 32-bit offsets/sizes
///   - Layout: [header][data][metadata]
///   - Endianness byte stored at END of file, just before metadata
///
/// Version 9-21:
///   - 20-byte header (SerializedFileHeader32) with 32-bit offsets/sizes
///   - Layout: [header][metadata][data]
///   - Endianness byte at offset 16 in header
///   - Limited to 4GB file sizes
///
/// Version >= 22 (kLargeFilesSupport):
///   - 48-byte header (SerializedFileHeader) with 64-bit offsets/sizes
///   - Layout: [header][metadata][data]
///   - Endianness byte at offset 40 in header
///   - Supports files larger than 4GB
///
/// Important: The header itself is always stored in big-endian format on disk,
/// but the m_Endianness byte indicates the endianness of the actual data section.
///
/// DEPRECATION WARNING: The deprecation process for Version <18 (Unity 5.5 and earlier) has started in Unity 6.5.
/// Initially this will be a warning, but upcoming versions of UnityDataTool and UnityFileSystem can be expected
/// to lose the ability to open and read those files (apart from low level information exposed by the
/// "serialized-file header" command).
/// </summary>
public static class SerializedFileDetector
{
    // Version boundaries for format changes
    // NOTE: This version is so old that it is extremely unlikely it will work with modern versions of Unity,
    // we handle it just for the purpose of trying to report accurate information about the file.
    private const uint NewLayoutVersion = 9;           // Changed from [header][data][metadata] to [header][metadata][data]

    private const uint LargeFilesSupportVersion = 22;  // Changed to 64-bit header

    // Minimum version for metadata section parsing (kTypeTreeNodeWithTypeFlags = 19, Unity 2019.1).
    // Older files have format differences that we do not attempt to support.
    private const uint MinMetadataParseVersion = 19;

    // Maximum version for metadata section parsing (kExtractedTypeTreeSupport = 23, Unity 6000.4).
    // Files newer than this version may have an unknown format and cannot be parsed safely.
    private const uint MaxMetadataParseVersion = 23;

    // Reasonable version range for SerializedFiles
    // Unity versions currently use values in the 20s-30s range
    private const uint MinVersion = 1;
    private const uint MaxVersion = 50;

    // Endianness values (only little-endian is supported in Unity 2023+)
    private const byte LittleEndian = 0;
    private const byte BigEndian = 1;

    // Header sizes
    private const int LegacyHeaderSize = 20;  // SerializedFileHeader32
    private const int ModernHeaderSize = 48;  // SerializedFileHeader

    // TypeTree section version boundaries
    private const uint SupportsRefObjectVersion = 20;        // m_RefTypes list (appears after externals)
    private const uint StoresTypeDependenciesVersion = 21;   // Per-type dependency list added
    private const uint ExtractedTypeTreeSupportVersion = 23; // TypeTree blob may be extracted externally

    // Per-type-entry constants
    private const int MonoBehaviourClassID = 114;    // persistentTypeID for MonoBehaviour
    private const int UndefinedPersistentTypeID = -1; // persistentTypeID for types with no known ClassID
    private const uint TypeTreeNodeSize = 32;         // Bytes per node in the blob (version >= 18)

    /// <summary>
    /// Attempts to detect if a file is a Unity SerializedFile by reading and validating its header.
    /// Returns false immediately if the file doesn't match the expected format.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <param name="info">If successful, contains header information</param>
    /// <returns>True if file appears to be a valid SerializedFile, false otherwise</returns>
    public static bool TryDetectSerializedFile(string filePath, out SerializedFileInfo info)
    {
        info = null;

        if (!File.Exists(filePath))
            return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            long fileLength = stream.Length;

            // Quick rejection: file must be at least large enough for the legacy header
            if (fileLength < LegacyHeaderSize)
                return false;

            // Read enough bytes to cover a modern header (48 bytes)
            // We'll determine which format to parse based on the version field
            byte[] headerBytes = new byte[ModernHeaderSize];
            int bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);

            if (bytesRead < LegacyHeaderSize)
                return false;

            // ============================================================
            // STEP 1: Read version to determine header format
            // ============================================================

            // The version field is always at offset 8 in both header formats.
            // The header itself is always stored in big-endian format on disk.
            // On little-endian platforms (Windows, etc.), we need to swap the header fields.
            //
            // We try both interpretations to determine if swapping is needed:
            uint versionLE = BitConverter.ToUInt32(headerBytes, 8);
            uint versionBE = BinaryFileHelper.SwapUInt32(versionLE);

            // Determine which interpretation gives us a valid version number
            uint version;
            bool needsSwap;  // Whether header fields need byte swapping (expected to be true when running on most modern systems, which are little-endian)

            if (versionLE >= MinVersion && versionLE <= MaxVersion)
            {
                // Reading as little-endian gives valid version (header is in little-endian format)
                version = versionLE;
                needsSwap = false;
            }
            else if (versionBE >= MinVersion && versionBE <= MaxVersion)
            {
                // Reading as big-endian gives valid version (header is in big-endian format)
                version = versionBE;
                needsSwap = true;
            }
            else
            {
                // Neither interpretation gives a valid version
                return false;
            }

            // Determine header format based on version
            bool isLegacyFormat = version < LargeFilesSupportVersion;

            // ============================================================
            // STEP 2: Read endianness byte
            // ============================================================
            //
            // The m_Endianness byte indicates the endianness of the DATA section
            // (not the header, which is always big-endian on disk).
            // Location depends on version:
            // - Version < 9:   At end of file (before metadata) - we skip reading it for detection
            // - Version 9-21:  At offset 16 in the 20-byte header
            // - Version >= 22: At offset 40 in the 48-byte header
            //
            // The endianness byte is never swapped (it's a single byte).

            byte endianness;

            if (version < NewLayoutVersion)
            {
                // Version < 9: Endianness is at the end of the file
                // For detection purposes, we infer it from the header byte order
                // (though this is technically the header's endianness, not the data's)
                endianness = needsSwap ? BigEndian : LittleEndian;
            }
            else if (isLegacyFormat)
            {
                // Version 9-21: Endianness is at offset 16 in SerializedFileHeader32
                if (bytesRead >= 17)
                {
                    endianness = headerBytes[16];

                    // Validate endianness value
                    if (endianness != LittleEndian && endianness != BigEndian)
                        return false;
                }
                else
                {
                    return false; // File truncated
                }
            }
            else
            {
                // Version >= 22: Endianness is at offset 40 in SerializedFileHeader
                if (bytesRead >= 41)
                {
                    endianness = headerBytes[40];

                    // Validate endianness value
                    if (endianness != LittleEndian && endianness != BigEndian)
                        return false;
                }
                else
                {
                    return false; // File truncated
                }
            }

            // ============================================================
            // STEP 3: Parse the appropriate header format
            // ============================================================

            ulong metadataSize, fileSize, dataOffset;

            if (isLegacyFormat)
            {
                // SerializedFileHeader32 Layout (20 bytes total):
                // Offset 0-3:   UInt32 m_MetadataSize
                // Offset 4-7:   UInt32 m_FileSize
                // Offset 8-11:  UInt32 m_Version
                // Offset 12-15: UInt32 m_DataOffset
                // Offset 16:    UInt8  m_Endianness (only present for version >= 9)
                // Offset 17-19: UInt8  m_Reserved[3]
                //
                // Note: For version < 9, m_Endianness is NOT in the header.
                //       It's stored at the end of the file, just before metadata begins.

                uint metadataSize32 = BinaryFileHelper.ReadUInt32(headerBytes, 0, needsSwap);
                uint fileSize32 = BinaryFileHelper.ReadUInt32(headerBytes, 4, needsSwap);
                uint dataOffset32 = BinaryFileHelper.ReadUInt32(headerBytes, 12, needsSwap);

                // Convert to 64-bit for consistency
                metadataSize = metadataSize32;
                fileSize = fileSize32;
                dataOffset = dataOffset32;

                // Special case: Legacy format used UInt32.MaxValue to indicate "unknown" file size
                if (fileSize32 == uint.MaxValue)
                {
                    fileSize = ulong.MaxValue;
                }
            }
            else
            {
                // SerializedFileHeader Layout (48 bytes total):
                // Offset 0-7:   UInt8[8] m_Legacy (unused, allows struct alignment with SerializedFileHeader32)
                // Offset 8-11:  UInt32   m_Version
                // Offset 12-15: UInt8[4] m_Reserved0 (explicit padding)
                // Offset 16-23: UInt64   m_MetadataSize
                // Offset 24-31: UInt64   m_FileSize
                // Offset 32-39: UInt64   m_DataOffset
                // Offset 40:    UInt8    m_Endianness
                // Offset 41-47: UInt8[7] m_Reserved1

                metadataSize = BinaryFileHelper.ReadUInt64(headerBytes, 16, needsSwap);
                fileSize = BinaryFileHelper.ReadUInt64(headerBytes, 24, needsSwap);
                dataOffset = BinaryFileHelper.ReadUInt64(headerBytes, 32, needsSwap);
            }

            // ============================================================
            // STEP 4: Validate header consistency
            // ============================================================

            // MetadataSize must not be the sentinel value (indicates corruption)
            if (metadataSize == ulong.MaxValue)
                return false;

            // DataOffset must be within the file size
            if (fileSize != ulong.MaxValue && dataOffset > fileSize)
                return false;

            // FileSize should roughly match actual file size
            // Allow some tolerance for "stream files" which can have padding
            if (fileSize != ulong.MaxValue)
            {
                // File size should not exceed actual file size by more than 1KB (arbitrary tolerance)
                if (fileSize > (ulong)fileLength + 1024)
                    return false;
            }

            // MetadataSize should be reasonable (not larger than the file itself)
            if (metadataSize > (ulong)fileLength)
                return false;

            // ============================================================
            // STEP 5: Populate and return info
            // ============================================================

            info = new SerializedFileInfo
            {
                Version = version,
                FileSize = fileSize,
                MetadataSize = metadataSize,
                DataOffset = dataOffset,
                Endianness = endianness,
                IsLegacyFormat = isLegacyFormat
            };

            return true;
        }
        catch
        {
            // Any exception during reading/parsing means this isn't a valid SerializedFile
            return false;
        }
    }

    /// <summary>
    /// Parses the metadata section from a previously-validated SerializedFile.
    ///
    /// The metadata starts immediately after the file header:
    ///   - Legacy format (version 9-21): header is 20 bytes
    ///   - Modern format (version >= 22): header is 48 bytes
    ///
    /// The metadata content is written in the endianness indicated by headerInfo.Endianness.
    /// All multi-byte integer fields are byte-swapped when that value is BigEndian (1).
    /// </summary>
    /// <param name="filePath">Path to the SerializedFile (must already have passed TryDetectSerializedFile)</param>
    /// <param name="headerInfo">Header info from a prior successful TryDetectSerializedFile call</param>
    /// <param name="metadata">On success, the parsed metadata; null on failure</param>
    /// <param name="errorMessage">On failure, a description of what went wrong; null on success</param>
    /// <returns>True if at least the initial metadata fields were successfully parsed</returns>
    public static bool TryParseMetadata(string filePath, SerializedFileInfo headerInfo, out SerializedFileMetadata metadata, out string errorMessage)
    {
        metadata = null;
        errorMessage = null;

        // Only support version >= 19 (Unity 2019.1). Older files have metadata format
        // differences we have not implemented.
        if (headerInfo.Version < MinMetadataParseVersion)
        {
            errorMessage = $"Metadata parsing is not supported for SerializedFile version {headerInfo.Version}. " +
                           $"Version {MinMetadataParseVersion} (Unity 2019.1) or newer is required.";
            return false;
        }

        // Reject versions beyond the highest known format. Future Unity versions may change the
        // metadata layout in ways that would cause incorrect results or a parse failure.
        // A newer version of UnityDataTool is required to read these files.
        if (headerInfo.Version > MaxMetadataParseVersion)
        {
            errorMessage = $"SerializedFile version {headerInfo.Version} is not supported. " +
                           $"UnityDataTool supports up to version {MaxMetadataParseVersion}. " +
                           $"Please use a newer version of UnityDataTool to read this file.";
            return false;
        }

        try
        {
            long metadataOffset = headerInfo.IsLegacyFormat ? LegacyHeaderSize : ModernHeaderSize;
            bool swap = headerInfo.Endianness == BigEndian;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.Seek(metadataOffset, SeekOrigin.Begin);
            using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

            // --- Field 1: Unity version string (null-terminated ASCII) ---
            string unityVersion = BinaryFileHelper.ReadNullTermString(reader);

            // An empty or unusually long version string indicates a corrupt file.
            // Even a stripped version string would be "0.0.0", not empty.
            if (unityVersion.Length == 0 || unityVersion.Length > 64)
            {
                errorMessage = $"Unity version string has unexpected length ({unityVersion.Length}).";
                return false;
            }

            // --- Field 2: Target platform (uint32) ---
            uint targetPlatform = BinaryFileHelper.ReadUInt32(reader, swap);

            // --- Field 3: Enable type tree flag (bool serialized as 1 byte) ---
            bool enableTypeTree = reader.ReadByte() != 0;

            metadata = new SerializedFileMetadata
            {
                UnityVersion = unityVersion,
                TargetPlatform = targetPlatform,
                EnableTypeTree = enableTypeTree,
            };

            // Parse the rest of the metadata section. Protected by its own try/catch so that any
            // failure there still returns a partially-populated metadata struct.
            ParseExtendedMetadata(reader, headerInfo, swap, metadataOffset, metadata);

            return true;
        }
        catch
        {
            errorMessage = "An unexpected error occurred while parsing the metadata section.";
            return false;
        }
    }

    /// <summary>
    /// Returns a diagnostic hint explaining why a SerializedFile may have failed to open,
    /// or null if no specific diagnosis is available.
    /// Currently detects the common case of missing TypeTrees (player builds compiled
    /// without type information, which the DLL reports as a generic unknown error).
    /// </summary>
    /// <param name="path">Real filesystem path to the file that failed to open.</param>
    public static string GetOpenFailureHint(string path)
    {
        if (TryDetectSerializedFile(path, out var fileInfo) &&
            TryParseMetadata(path, fileInfo, out var metadata, out _) &&
            !metadata.EnableTypeTree)
        {
            return "Note: This file does not have TypeTrees and can only be opened if all the " +
                   "types it uses exactly match the types in the build of UnityFileSystemApi being used.";
        }
        return null;
    }

    /// <summary>
    /// Parses the TypeTree and other arrays that are stored in the metadata,
    /// </summary>
    private static void ParseExtendedMetadata(BinaryReader reader, SerializedFileInfo headerInfo,
        bool swap, long metadataOffset, SerializedFileMetadata metadata)
    {
        try
        {
            uint version = headerInfo.Version;
            bool enableTypeTree = metadata.EnableTypeTree;
            Stream stream = reader.BaseStream;

            // --- Regular type list (m_Types) ---
            int typeCount = BinaryFileHelper.ReadInt32(reader, swap);
            metadata.TypeTreeCount = typeCount;

            var typeTrees = new TypeTreeInfo[typeCount];
            for (int i = 0; i < typeCount; i++)
                typeTrees[i] = ReadTypeEntry(reader, version, swap, isRefType: false, enableTypeTree);
            metadata.TypeTrees = typeTrees;

            // --- Object list ---
            // Per-object layout (version >= 19):
            //   [4-byte alignment relative to metadata start]
            //   [int64  fileID]
            //   [uint32 byteStart]  or  [uint64 byteStart]  (version >= 22)
            //   [uint32 byteSize]
            //   [uint32 typeID]
            int objectCount = BinaryFileHelper.ReadInt32(reader, swap);
            var objectList = new ObjectInfo[objectCount];
            for (int i = 0; i < objectCount; i++)
            {
                BinaryFileHelper.AlignTo4(stream, metadataOffset);
                long fileID = BinaryFileHelper.ReadInt64(reader, swap);
                // byteStart is relative to the data section; add DataOffset to get the absolute file offset,
                // matching the behaviour of the native DLL which returns the absolute offset in ObjectInfo.Offset.
                long byteStart = version >= LargeFilesSupportVersion
                    ? (long)BinaryFileHelper.ReadUInt64(reader, swap)
                    : BinaryFileHelper.ReadUInt32(reader, swap);
                byteStart += (long)headerInfo.DataOffset;
                long byteSize = BinaryFileHelper.ReadUInt32(reader, swap);
                // typeIndex is a 0-based index into the m_Types array, not the persistent type ID.
                // Resolve it to the persistent type ID to match the behaviour of the native DLL.
                int typeIndex = BinaryFileHelper.ReadInt32(reader, swap);
                int persistentTypeID = (typeIndex >= 0 && typeIndex < typeTrees.Length)
                    ? typeTrees[typeIndex].PersistentTypeID
                    : typeIndex;
                objectList[i] = new ObjectInfo(fileID, byteStart, byteSize, persistentTypeID);
            }
            metadata.ObjectList = objectList;

            // --- Script type list ---
            // Each entry points to the MonoScript object that backs a C# MonoBehaviour-derived type.
            // Per-entry layout (version >= 14, applies to all our versions):
            //   [int32 localSerializedFileIndex]   (FileID: 0 = this file, 1+ = external ref index)
            //   [4-byte alignment relative to metadata start]
            //   [int64 localIdentifierInFile]      (PathID: object ID within the identified file)
            int scriptTypeCount = BinaryFileHelper.ReadInt32(reader, swap);
            var scriptTypes = new ScriptType[scriptTypeCount];
            for (int i = 0; i < scriptTypeCount; i++)
            {
                int fileID = BinaryFileHelper.ReadInt32(reader, swap);
                BinaryFileHelper.AlignTo4(stream, metadataOffset);
                long pathID = BinaryFileHelper.ReadInt64(reader, swap);
                scriptTypes[i] = new ScriptType { FileID = fileID, PathID = pathID };
            }
            metadata.ScriptTypes = scriptTypes;

            // --- External references list ---
            // Per-entry layout:
            //   [null-terminated string tempEmpty]
            //   [uint32[4] guid]   (16 bytes)
            //   [int32 type]
            //   [null-terminated string pathName]
            int externalsCount = BinaryFileHelper.ReadInt32(reader, swap);
            var externalRefs = new ExternalReference[externalsCount];
            for (int i = 0; i < externalsCount; i++)
            {
                BinaryFileHelper.ReadNullTermString(reader);  // tempEmpty (empty in practice)
                var guid = BinaryFileHelper.ReadHash128(reader, swap);
                int typeInt = BinaryFileHelper.ReadInt32(reader, swap);
                string pathName = BinaryFileHelper.ReadNullTermString(reader);
                externalRefs[i] = new ExternalReference
                {
                    Path = pathName,
                    Guid = guid.ToString(),
                    Type = (ExternalReferenceType)typeInt,
                };
            }
            metadata.ExternalReferences = externalRefs;

            // m_RefTypes (version >= 20) is not located immediately after m_Types.
            // It appears at the end of the metadata section
            if (version < SupportsRefObjectVersion)
                return;

            // --- SerializeReference type list (m_RefTypes, version >= 20) ---
            int refTypeCount = BinaryFileHelper.ReadInt32(reader, swap);
            metadata.SerializedReferenceTypeTreeCount = refTypeCount;

            var refTypeTrees = new TypeTreeInfo[refTypeCount];
            for (int i = 0; i < refTypeCount; i++)
                refTypeTrees[i] = ReadTypeEntry(reader, version, swap, isRefType: true, enableTypeTree);
            metadata.SerializedReferenceTypeTrees = refTypeTrees;
        }
        catch
        {
            // Best-effort: leave metadata partially populated with whatever was parsed
            // successfully before the failure.
        }
    }

    /// <summary>
    /// Reads one type entry from the metadata stream into a <see cref="TypeTreeInfo"/>.
    /// Advances the stream past all fields, including the TypeTree blob when present.
    ///
    /// Per-entry layout:
    ///   [int32  persistentTypeID]
    ///   [uint8  isStrippedType]
    ///   [int16  scriptTypeIndex]
    ///   [Hash128 scriptID]        (conditional — see below)
    ///   [Hash128 oldTypeHash]
    ///   if enableTypeTree:
    ///     if version >= 23:
    ///       [Hash128 typeTreeContentHash]
    ///       [uint32  typeTreeSize]      (0 = blob extracted to external store)
    ///     [TypeTree blob]               (present when version < 23 or typeTreeSize > 0)
    ///     if version >= 21:
    ///       if isRefType:  [string className] [string nameSpace] [string asmName]
    ///       else:          [int32 depCount] [int32 * depCount]
    /// </summary>
    private static TypeTreeInfo ReadTypeEntry(BinaryReader reader, uint version, bool swap,
        bool isRefType, bool enableTypeTree)
    {
        var info = new TypeTreeInfo();
        Stream stream = reader.BaseStream;

        // persistentTypeID: the Unity ClassID. -1 (UndefinedPersistentTypeID) when the
        // class has no known built-in ClassID (e.g. an unresolved script type).
        info.PersistentTypeID = BinaryFileHelper.ReadInt32(reader, swap);

        // isStrippedType: true when the type definition was stripped from the build.
        // Objects of a stripped type cannot be fully deserialized without a matching runtime.
        info.IsStrippedType = reader.ReadByte() != 0;

        // scriptTypeIndex: index into the file's MonoScript reference list. -1 = not a script type.
        info.ScriptTypeIndex = BinaryFileHelper.ReadInt16(reader, swap);

        // scriptID is a 128-bit hash identifying a MonoScript (MD4 of assembly + namespace + class name).
        // It is present for:
        //   - Types with no known ClassID  (persistentTypeID == UndefinedPersistentTypeID)
        //   - MonoBehaviour types          (persistentTypeID == 114)
        //   - Script-backed types          (scriptTypeIndex >= 0)
        //
        // Historical note: files written before Unity 2018.3.0a1 omitted scriptID when
        // scriptTypeIndex >= 0. All files this parser supports are version >= 19 (Unity 2019.1+),
        // so that historical case never applies here.
        bool hasScriptID = info.PersistentTypeID == UndefinedPersistentTypeID
                        || info.PersistentTypeID == MonoBehaviourClassID
                        || info.ScriptTypeIndex >= 0;
        if (hasScriptID)
            info.ScriptID = BinaryFileHelper.ReadHash128(reader, swap);

        // oldTypeHash: always present. Hash of the TypeTree content as originally written.
        info.TypeTreeStructureHash = BinaryFileHelper.ReadHash128(reader, swap);

        if (!enableTypeTree)
            return info;

        // --- TypeTree blob ---

        uint typeTreeSize = 0;
        if (version >= ExtractedTypeTreeSupportVersion)
        {
            // Version >= 23: a 20-byte prefix precedes the blob.
            // typeTreeContentHash is used as a cache key for the TypeTree store.
            // typeTreeSize == 0 means the blob was extracted to an external archive.
            info.TypeTreeContentHash = BinaryFileHelper.ReadHash128(reader, swap);
            typeTreeSize = BinaryFileHelper.ReadUInt32(reader, swap);
            info.TypeTreeSerializedSize = typeTreeSize;
        }

        bool blobPresent = version < ExtractedTypeTreeSupportVersion || typeTreeSize > 0;
        if (blobPresent)
        {
            if (version < ExtractedTypeTreeSupportVersion)
            {
                // Versions 19-22: blob begins directly with [uint32 numberOfNodes][uint32 numberOfChars],
                // followed by a flat array of 32-byte nodes and a packed string buffer.
                uint numberOfNodes = BinaryFileHelper.ReadUInt32(reader, swap);
                uint numberOfChars = BinaryFileHelper.ReadUInt32(reader, swap);
                uint dataBytes = numberOfNodes * TypeTreeNodeSize + numberOfChars;
                stream.Seek(dataBytes, SeekOrigin.Current);
                // Record the total blob size including the 8-byte count header.
                info.TypeTreeSerializedSize = 8 + dataBytes;
            }
            else
            {
                // Version >= 23 with inline blob: skip exactly typeTreeSize bytes.
                // The blob starts with its own 8-byte magic+version prefix, followed by
                // node count, char count, node array, and string buffer.
                stream.Seek(typeTreeSize, SeekOrigin.Current);
            }
            info.InlineTypeTree = true;
        }

        if (version >= StoresTypeDependenciesVersion)
        {
            if (isRefType)
            {
                // SerializeReference entries carry their type identity strings here.
                info.ClassName = BinaryFileHelper.ReadNullTermString(reader);
                info.Namespace = BinaryFileHelper.ReadNullTermString(reader);
                info.AssemblyName = BinaryFileHelper.ReadNullTermString(reader);
            }
            else
            {
                // Regular type entries carry indices into the m_RefTypes pool, identifying
                // which SerializeReference types objects of this type may hold.
                int depCount = BinaryFileHelper.ReadInt32(reader, swap);
                var deps = new int[depCount];
                for (int j = 0; j < depCount; j++)
                    deps[j] = BinaryFileHelper.ReadInt32(reader, swap);
                info.TypeDependencies = deps;
            }
        }

        return info;
    }

}
