# dump Command

The `dump` command converts Unity SerializedFiles into human-readable text format. This is useful for inspecting the internal structure and properties of Unity assets.

## Quick Reference

```
UnityDataTool dump <path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<path>` | Path to file to dump | *(required)* |
| `-o, --output-path <path>` | Output folder | Current folder |
| `--stdout` | Write the dump to stdout (status and errors go to stderr). Mutually exclusive with `-o`. | `false` |
| `-f, --output-format <format>` | Output format | `text` |
| `-s, --skip-large-arrays` | Skip dumping large arrays | `false` |
| `-i, --objectid <id>` | Only dump object with this ID | All objects |
| `-t, --type <type>` | Filter by object type (ClassID number or type name) | All objects |
| `-d, --typetree-data <file>` | Load an external TypeTree data file before processing (Unity 6.5+) | — |

## Examples

Dump all objects in a file to the current directory:
```bash
UnityDataTool dump /path/to/file
```

Dump to a specific output folder:
```bash
UnityDataTool dump /path/to/file -o /path/to/output
```

Dump a single object by ID:
```bash
UnityDataTool dump /path/to/file -i 1234567890
```

Skip large arrays for cleaner output:
```bash
UnityDataTool dump /path/to/file -s
```

Dump only MonoBehaviour objects by type name:
```bash
UnityDataTool dump /path/to/file -t MonoBehaviour
```

Same thing using the numeric ClassID:
```bash
UnityDataTool dump /path/to/file -t 114
```

Dump the AssetBundle manifest object:
```bash
UnityDataTool dump mybundle -t AssetBundle
```

Write the dump to stdout and pipe it through another tool:
```bash
UnityDataTool dump /path/to/file --stdout | grep "ClassID: 114"
```

---

## Writing to stdout

Use `--stdout` to send the dump to standard output instead of writing a `.txt` file. Status messages (the `Processing ...` line, errors, and stack traces) are routed to stderr so the dump on stdout is clean for piping or redirecting.

```bash
UnityDataTool dump /path/to/file --stdout > my-dump.txt
```

Restrictions:

- `--stdout` and `-o` are mutually exclusive.
- For Unity archives that contain more than one SerializedFile, `--stdout` is refused — there is no unambiguous way to deliver multiple files on a single stream. Pass an individual SerializedFile, or omit `--stdout` to get one `.txt` per SerializedFile in the output folder.

---

## Filtering by Type

The `-t` / `--type` option filters output to objects of a specific Unity type. It accepts either a numeric ClassID (e.g. `114`) or a type name (e.g. `MonoBehaviour`). Type name matching is case-insensitive.

This is particularly useful for inspecting MonoBehaviour data in built AssetBundles. MonoBehaviour and ScriptableObject field values are serialized as binary, and a typical bundle contains many other object types (meshes, textures, materials, etc.). Using `-t MonoBehaviour` dumps only the scripting objects, showing the serialized C# field names, types, and values.

---

## Archive Support

When you pass an Archive file (like an AssetBundle), the command dumps all SerializedFiles inside.

**Example:** For an AssetBundle `scenes.bundle` containing two scenes:

```bash
UnityDataTool dump scenes.bundle
```

**Output files:**
```
BuildPlayer-SampleScene.sharedAssets.txt
BuildPlayer-SampleScene.txt
BuildPlayer-Scene2.sharedAssets.txt
BuildPlayer-Scene2.txt
```

---

## TypeTree Requirement

Unity's binary SerializedFile format stores objects as raw binary blobs. TypeTrees are schema metadata embedded in the file that describe the layout of each type — field names, data sizes, and alignment. The `dump` command requires TypeTrees to interpret those blobs and produce readable output.

**When are TypeTrees absent?**

TypeTrees are included by default. They are stripped in two common situations:

- **Player builds** with *Strip Engine Code* or similar size-reduction options enabled.
- **AssetBundles** built with the *Disable Write TypeTree* build option.

**Error when TypeTrees are missing:**

Various errors can be caused by missing TypeTrees, including:

```
ArgumentException: Invalid object id
```

**Tip:** Use `serialized-file metadata` to confirm whether a file has TypeTrees:

```bash
UnityDataTool serialized-file metadata /path/to/file
```

The `TypeTree Definitions` field will show `No` when TypeTrees are absent.

**External TypeTree data (Unity 6.5+):**

If your bundles were built with TypeTree data extracted to a separate file, use the `--typetree-data` option to load it:

```bash
UnityDataTool dump /path/to/file.bundle --typetree-data /path/to/typetree.bin
```

---

## Output Format

The output is similar to Unity's `binary2text` tool. Unfiltered dumps begin with external references (when filtering with `-i` or `-t` this section is omitted — use the [`serialized-file externalrefs`](#) command if you want them separately):

```
External References
path(1): "Library/unity default resources" GUID: 0000000000000000e000000000000000 Type: 0
path(2): "Resources/unity_builtin_extra" GUID: 0000000000000000f000000000000000 Type: 0
path(3): "archive:/CAB-35fce856128a6714740898681ea54bbe/..." GUID: 00000000000000000000000000000000 Type: 0
```

Followed by object entries:

```
ID: -8138362113332287275 (ClassID: 135) SphereCollider 
  m_GameObject PPtr<GameObject> 
    m_FileID int 0
    m_PathID SInt64 -1473921323670530447
  m_Material PPtr<PhysicMaterial> 
    m_FileID int 0
    m_PathID SInt64 0
  m_IsTrigger bool False
  m_Enabled bool True
  m_Radius float 0.5
  m_Center Vector3f 
    x float 0
    y float 0
    z float 0
```

**Refer to the [TextDumper documentation](textdumper.md) for detailed output format explanation.**

---

## Understanding PPtrs

PPtrs (Property Pointers) are Unity's mechanism for referencing objects:

| Field | Description |
|-------|-------------|
| `m_FileID` | Index into External References list (0 = same file) |
| `m_PathID` | Object's Local File Identifier (LFID) in that file |

A null reference will have value m_FileID = 0, m_PathID = 0

The external reference table is used to resolve cross-file references. It always starts at index 1.  m_FileID 0 is used for references within the current file.



