# serialized-file Command

The `serialized-file` command (alias: `sf`) provides utilities for quickly inspecting [SerializedFile](unity-content-format.md#serializedfile) metadata without performing a full analysis.

This exposes information about the Binary SerializedFile format.  This format has evolved over time, but all recent versions have 
* a small header section (exposed by the `header` subcommand)
* a metadata section which contains summary of the data
  * Unity Version and target platform
  * typetree information
  * the list of objects and offsets
  * external references
* the data section which contains the Unity objects in serialized form

The 'externalrefs' and 'objectlist' sub-commands expose information from the metadata section.
The `dump` command can be used to view the serialized objects.

## Sub-Commands

| Sub-Command | Description |
|-------------|-------------|
| [`externalrefs`](#externalrefs) | List external file references |
| [`objectlist`](#objectlist) | List all objects in the file |
| [`header`](#header) | Show SerializedFile header information |
| [`metadata`](#metadata) | Show SerializedFile metadata (Unity version, platform, TypeTree summary) |

---

## externalrefs

Lists the external file references (dependencies) in a SerializedFile. This shows which other files the SerializedFile depends on.

### Quick Reference

```
UnityDataTool serialized-file externalrefs <filename> [options]
UnityDataTool sf externalrefs <filename> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<filename>` | Path to the SerializedFile | *(required)* |
| `-f, --format <format>` | Output format: `Text` or `Json` | `Text` |

### Example - Text Output

```bash
UnityDataTool serialized-file externalrefs level0
```

**Output:**
```
Index: 1, Path: globalgamemanagers.assets
Index: 2, Path: sharedassets0.assets
Index: 3, Path: Library/unity default resources
```

### Example - JSON Output

```bash
UnityDataTool sf externalrefs sharedassets0.assets --format json
```

**Output:**
```json
[
  {
    "index": 1,
    "path": "globalgamemanagers.assets",
    "guid": "00000000000000000000000000000000",
    "type": "NonAssetType"
  },
  {
    "index": 2,
    "path": "Library/unity default resources",
    "guid": "0000000000000000e000000000000000",
    "type": "NonAssetType"
  }
]
```

---

## objectlist

Lists all objects contained in a SerializedFile, showing their IDs, types, offsets, and sizes.

### Quick Reference

```
UnityDataTool serialized-file objectlist <filename> [options]
UnityDataTool sf objectlist <filename> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<filename>` | Path to the SerializedFile | *(required)* |
| `-f, --format <format>` | Output format: `Text` or `Json` | `Text` |

### Example - Text Output

```bash
UnityDataTool sf objectlist sharedassets0.assets
```

**Output:**
```
Id                   Type                                     Offset          Size           
------------------------------------------------------------------------------------------
1                    PreloadData                              83872           49             
2                    Material                                 83936           268            
3                    Shader                                   84208           6964           
4                    Cubemap                                  91184           240            
5                    MonoBehaviour                            91424           60             
6                    MonoBehaviour                            91488           72             
```

### Example - JSON Output

```bash
UnityDataTool serialized-file objectlist level0 --format json
```

**Output:**
```json
[
  {
    "id": 1,
    "typeId": 1,
    "typeName": "GameObject",
    "offset": 4864,
    "size": 132
  },
  {
    "id": 2,
    "typeId": 4,
    "typeName": "Transform",
    "offset": 5008,
    "size": 104
  }
]
```

---

## header

Shows the SerializedFile header information. This is useful for testing whether a file is a valid SerializedFile and for inspecting the version and structure.

### Quick Reference

```
UnityDataTool serialized-file header <filename> [options]
UnityDataTool sf header <filename> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<filename>` | Path to the SerializedFile | *(required)* |
| `-f, --format <format>` | Output format: `Text` or `Json` | `Text` |

### Example - Text Output

```bash
UnityDataTool sf header sharedassets0.assets
```

**Output:**
```
Version              22
Format               Modern (64-bit)
File Size            1,234,567 bytes
Metadata Size        45,678 bytes
Data Offset          45,728
Endianness           Little Endian
```

### Example - JSON Output

```bash
UnityDataTool serialized-file header level0 --format json
```

**Output:**
```json
{
  "version": 22,
  "format": "Modern (64-bit)",
  "fileSize": 31988,
  "metadataSize": 24580,
  "dataOffset": 24640,
  "endianness": "Little Endian"
}
```

### Header Fields

| Field | Description |
|-------|-------------|
| **Version** | SerializedFile format version. Modern Unity (2019+) uses version 22+. |
| **Format** | Header format type: "Legacy (32-bit)" for versions < 22, or "Modern (64-bit)" for versions ≥ 22. Modern format supports files larger than 4GB. |
| **File Size** | Total size of the SerializedFile in bytes. Padding might make the actual file size slightly larger. |
| **Metadata Size** | Size of the metadata section containing type information and object indices. |
| **Data Offset** | Byte offset where the object data section begins in the file. |
| **Endianness** | Byte order of the data in the file: "Little Endian" (x86, most platforms) or "Big Endian" (older console platforms). |

---

## metadata

Shows information from the metadata section of a SerializedFile. This includes the Unity version, target platform, TypeTree storage mode (inline, external, or absent), and counts of the type entries recorded in the file. The JSON output includes additional per-type details; see the notes below.

Requires SerializedFile version 19 (Unity 2019.1) or newer. Files older than version 19 are not supported by this subcommand.

### Quick Reference

```
UnityDataTool serialized-file metadata <filename> [options]
UnityDataTool sf metadata <filename> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<filename>` | Path to the SerializedFile | *(required)* |
| `-f, --format <format>` | Output format: `Text` or `Json` | `Text` |

### Example - Text Output

```bash
UnityDataTool sf metadata level0
```

**Output:**
```
Unity Version        6000.0.65f1
Target Platform      19
TypeTree Definitions No
TypeTree Count       6
RefType Count        0
```

### Example - JSON Output

```bash
UnityDataTool serialized-file metadata level0 --format json
```

**Output (top-level fields shown; per-type arrays omitted for brevity):**
```json
{
  "unityVersion": "6000.0.65f1",
  "targetPlatform": 19,
  "enableTypeTree": false,
  "typeTreeCount": 6,
  "serializedReferenceTypeTreeCount": 0,
  "typeTrees": [ ... ],
  "serializedReferenceTypeTrees": [ ... ],
  "scriptTypes": [ ... ]
}
```

Each element of `typeTrees` and `serializedReferenceTypeTrees` contains per-type details including hash values, TypeTree blob size, inline/external flag, and (for SerializeReference types) the C# class identity. Each element of `scriptTypes` contains the file and object ID of the backing MonoScript asset.

### Metadata Fields

The text and JSON outputs use different field names and representations for some fields.

| Text Field | JSON Field | Description |
|------------|------------|-------------|
| **Unity Version** | `unityVersion` | The Unity version string used to build this file (e.g. `"2022.1.20f1"`, `"6000.0.65f1"`). |
| **Target Platform** | `targetPlatform` | Numeric platform identifier. Common values: `2` = OSX Standalone, `9` = iOS, `13` = Android, `19` = Windows Standalone x64. See [BuildTarget](https://docs.unity3d.com/ScriptReference/BuildTarget.html) for details. |
| **TypeTree Definitions** | `enableTypeTree` | Whether TypeTree blobs are stored in this file. The text output derives a descriptive string from the raw boolean and the parsed type entries; the JSON output exposes the raw boolean directly. Text values: `No` — TypeTrees absent (default Player build); `Inline` — all TypeTree blobs are embedded in the file (Editor and TypeTree-enabled builds); `External` — TypeTree blobs were extracted to a separate store (version ≥ 23); `Mixed` — entries disagree (unexpected; indicates a parser or file anomaly); `Unknown` — `enableTypeTree` is true but no type entries were parsed. TypeTrees are required for the `objectlist` and `externalrefs` subcommands to show type names. |
| **TypeTree Count** | `typeTreeCount` | Number of regular (object) type entries recorded in the file. Present even when TypeTree definitions are not stored inline. |
| **RefType Count** | `serializedReferenceTypeTreeCount` | Number of type entries for `[SerializeReference]` types recorded in the file. Always `0` for files with version < 20. |
| *(JSON only)* | `typeTrees` | Array of per-type detail objects for the regular type entries. `null` when parsing failed or was not attempted. See **Per-Type Entry Fields** below. |
| *(JSON only)* | `serializedReferenceTypeTrees` | Array of per-type detail objects for the `[SerializeReference]` type entries. Empty array for files with version < 20. See **Per-Type Entry Fields** below. |
| *(JSON only)* | `scriptTypes` | Array of MonoScript references for the C# types used in this file. Each entry's index corresponds to the `scriptTypeIndex` field of a type entry in `typeTrees`. See **Script Type Entry Fields** below. |

### Per-Type Entry Fields

Each element of `typeTrees` and `serializedReferenceTypeTrees` in the JSON output contains the following fields:

| JSON Field | Description |
|------------|-------------|
| `persistentTypeID` | Unity ClassID (e.g. `114` = MonoBehaviour). `-1` for `[SerializeReference]` entries whose type has no built-in ClassID. |
| `isStrippedType` | `true` for types representing prefab-stripped objects (the `stripped` keyword in YAML). Orthogonal to TypeTree presence. |
| `scriptTypeIndex` | Index into the file's MonoScript reference list. `-1` for native Unity types. |
| `scriptID` | 128-bit hash (MD4 of assembly + namespace + class name) identifying the MonoScript. All-zeros when not applicable. |
| `typeTreeStructureHash` | MD4 hash of the TypeTree structure as originally written; used for compatibility checking at load time. |
| `typeTreeContentHash` | XXH3 hash of the TypeTree blob. All-zeros for files with version < 23. |
| `typeTreeSerializedSize` | Byte size of the TypeTree blob for this entry. `0` when `inlineTypeTree` is false. |
| `inlineTypeTree` | `true` when the TypeTree blob is present inline in the file's metadata. |
| `className` | C# class name; non-empty only for `[SerializeReference]` entries (version ≥ 21). |
| `namespaceName` | C# namespace; non-empty only for `[SerializeReference]` entries (version ≥ 21). |
| `assemblyName` | Assembly name; non-empty only for `[SerializeReference]` entries (version ≥ 21). |
| `typeDependencies` | Array of indices into `serializedReferenceTypeTrees` listing which `[SerializeReference]` types objects of this type may hold. Empty for `[SerializeReference]` entries or files with version < 21. |

### Script Type Entry Fields

Each element of `scriptTypes` in the JSON output contains:

| JSON Field | Description |
|------------|-------------|
| `fileID` | Index into the file's external references list identifying which SerializedFile contains the MonoScript asset. `0` = this file itself; `1`+ = 1-based index into the `externalrefs` list. |
| `pathID` | The object ID (`localIdentifierInFile`) of the MonoScript within the identified file. Corresponds to the `id` field shown by `sf objectlist` on that file. |

Notes:

* For SerializedFiles inside AssetBundles the Unity Version can be stripped, in which case the Unity Version value is "0.0.0".  See [BuildAssetBundleOptions.AssetBundleStripUnityVersion](https://docs.unity3d.com/ScriptReference/BuildAssetBundleOptions.AssetBundleStripUnityVersion.html).
* For AssetBundles where the typetrees have been stripped out the version string takes the form `<unity-version>\n<assetbundle-format-version>`.  The assetbundle-format-version rarely changes, and is currently 2. Examples "6000.0.65f1\n2" and "0.0.0\n2".
* The Unity Editor will attempt to load SerializedFiles regardless of the Platform.  But the Runtime will only load files built with the correct platform value.

---

## Use Cases

### Quick File Inspection

Use `serialized-file` when you need quick information about a SerializedFile without generating a full SQLite database:

```bash
# Check file format and version
UnityDataTool sf header level0

# Check Unity version, target platform, and TypeTree flag
UnityDataTool sf metadata level0

# Check what objects are in a file
UnityDataTool sf objectlist sharedassets0.assets

# Check file dependencies
UnityDataTool sf externalrefs level0
```

### Scripting and Automation

The JSON output format is ideal for scripts and automated processing:

```bash
# Extract object count
UnityDataTool sf objectlist level0 -f json | jq 'length'

# Find specific object types
UnityDataTool sf objectlist sharedassets0.assets -f json | jq '.[] | select(.typeName == "Material")'
```

---

## SerializedFile vs Archive

When working with AssetBundles (or a compressed Player build) you need to extract the contents first (with `archive extract`), then run the `serialized-file` command on individual files in the extracted output.

**Example workflow:**
```bash
# 1. List contents of an archive
UnityDataTool archive list scenes.bundle

# 2. Extract the archive
UnityDataTool archive extract scenes.bundle -o extracted/

# 3. Inspect individual SerializedFiles
UnityDataTool sf objectlist extracted/CAB-5d40f7cad7c871cf2ad2af19ac542994
```

---

## Notes

- This command only supports extracting information from the SerializedFile header of individual files.  It does not extract detailed type-specific properties.  Use `analyze` for full analysis of one or more SerializedFiles.
- The command uses the same native library (UnityFileSystemApi) as other UnityDataTool commands, ensuring consistent file reading across all Unity versions.

