# archive Command

The `archive` command provides utilities for working with Unity Archives (AssetBundles and web platform `.data` files).

## Sub-Commands

| Sub-Command | Description |
|-------------|-------------|
| [`list`](#list) | List contents of an archive |
| [`extract`](#extract) | Extract contents of an archive |

---

## list

Lists the SerializedFiles contained within an archive.

### Quick Reference

```
UnityDataTool archive list <archive-path>
```

### Example

```bash
UnityDataTool archive list scenes.bundle
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

### Example

```bash
UnityDataTool archive extract scenes.bundle -o contents
```

**Output files:**
```
contents/BuildPlayer-SampleScene.sharedAssets
contents/BuildPlayer-SampleScene
contents/BuildPlayer-Scene2.sharedAssets
contents/BuildPlayer-Scene2
```

> **Note:** The extracted files are binary SerializedFiles, not text. Use the [`dump`](command-dump.md) command to convert them to readable text format.

---

## Comparison: extract vs dump

| Command | Output | Use Case |
|---------|--------|----------|
| `archive extract` | Binary SerializedFiles, .resS anything else inside the archive content | When you need all the raw files inside an archive |
| `dump` | text | When you want to inspect object content |

The `dump` command can directly process archives without extracting first.

