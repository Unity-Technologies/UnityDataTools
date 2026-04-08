# archive Command

The `archive` command provides utilities for working with Unity Archives (AssetBundles and web platform `.data` files).

## Sub-Commands

| Sub-Command | Description |
|-------------|-------------|
| [`info`](#info) | Display a high-level summary |
| [`header`](#header) | Display archive header information |
| [`blocks`](#blocks) | Display the data block list |
| [`list`](#list) | List contents of an archive |
| [`extract`](#extract) | Extract contents of an archive |

---

## info

Displays a high-level summary of a Unity Archive file, including compression ratio, file counts, and data sizes.

### Quick Reference

```
UnityDataTool archive info <archive-path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<archive-path>` | Path to the archive file | *(required)* |
| `-f, --format <Text\|Json>` | Output format | `Text` |

### Example

```bash
UnityDataTool archive info scenes.bundle
UnityDataTool archive info scenes.bundle -f Json
```

---

## header

Displays the header information of a Unity Archive file, including format version, Unity version, file size, metadata compression, and archive flags.

Very old versions of the Unity Archive format are not supported.  But the files created by all currently supported Unity versions should be compatible (and it was tested with files as old as Unity 2017).

### Quick Reference

```
UnityDataTool archive header <archive-path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<archive-path>` | Path to the archive file | *(required)* |
| `-f, --format <Text\|Json>` | Output format | `Text` |

### Example

```bash
UnityDataTool archive header scenes.bundle
UnityDataTool archive header scenes.bundle -f Json
```

---

## blocks

Displays the data block list of a Unity Archive file, showing the size, compression type, and file offset of each block. 

Very old versions of the Unity Archive format are not supported.

### Quick Reference

```
UnityDataTool archive blocks <archive-path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<archive-path>` | Path to the archive file | *(required)* |
| `-f, --format <Text\|Json>` | Output format | `Text` |

### Example

```bash
UnityDataTool archive blocks scenes.bundle
UnityDataTool archive blocks scenes.bundle -f Json
```

---

## list

Lists the contents of an archive, including the offset, size, and flags of each file.

Very old versions of the Unity Archive format are not supported.

### Quick Reference

```
UnityDataTool archive list <archive-path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<archive-path>` | Path to the archive file | *(required)* |
| `-f, --format <Text\|Json>` | Output format | `Text` |

### Example

```bash
UnityDataTool archive list scenes.bundle
UnityDataTool archive list scenes.bundle -f Json
```

---

## extract

Extracts the contents of an archive to disk. This is similar to Unity's `WebExtract` tool.

### Quick Reference

```
UnityDataTool archive extract <archive-path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<archive-path>` | Path to the archive file | *(required)* |
| `-o, --output-path <path>` | Output directory | `archive` |
| `--filter <text>` | Case-insensitive substring filter on file paths inside the archive | *(none — extract all)* |

### Example

```bash
UnityDataTool archive extract scenes.bundle -o contents
UnityDataTool archive extract scenes.bundle --filter sharedAssets
```

**Output files:**
```
contents/BuildPlayer-SampleScene.sharedAssets
contents/BuildPlayer-SampleScene
contents/BuildPlayer-Scene2.sharedAssets
contents/BuildPlayer-Scene2
```

> **Note:** The extracted files are in binary formats, not text.  If they are SerializedFiles then use the [`dump`](command-dump.md) command to convert them to readable text format.  See also the [`serialized-file`](command-serialized-file.md) command.

---

## Comparison: extract vs dump

| Command | Output | Use Case |
|---------|--------|----------|
| `archive extract` | Binary SerializedFiles, .resS anything else inside the archive content | When you need all the raw files inside an archive |
| `dump` | text | When you want to inspect object content |

The `dump` command can directly process archives without extracting first.

