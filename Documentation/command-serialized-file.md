# serialized-file Command

The `serialized-file` command (alias: `sf`) provides utilities for quickly inspecting SerializedFile metadata without performing a full analysis.

## Sub-Commands

| Sub-Command | Description |
|-------------|-------------|
| [`externalrefs`](#externalrefs) | List external file references |
| [`objectlist`](#objectlist) | List all objects in the file |

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

## Use Cases

### Quick File Inspection

Use `serialized-file` when you need quick information about a SerializedFile without generating a full SQLite database:

```bash
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

