# archive Command

The `archive` command provides utilities for working with [Unity Archives](unity-content-format.md#unity-archive) — container files used for AssetBundles, compressed Player builds (`data.unity3d`), and web platform `.data` files. Archives hold one or more files (typically SerializedFiles) and may apply compression to the content. See [Player Build Format](playerbuild-format.md) for how these containers are used in Player builds.

To inspect the serialized objects *inside* an archive, use the [`dump`](command-dump.md#archive-support) command, which can open archives directly without extracting first.

## Sub-Commands

| Sub-Command | Description | Unity Archive | Web `.data` |
|-------------|-------------|:---:|:---:|
| [`info`](#info) | Display a high-level summary | Yes | — |
| [`header`](#header) | Display archive header information | Yes | — |
| [`blocks`](#blocks) | Display the data block list | Yes | — |
| [`list`](#list) | List contents of an archive | Yes | Yes |
| [`extract`](#extract) | Extract contents of an archive | Yes | Yes |
| [`repack`](#repack) | Rewrite an archive with a different LZ4/LZ4HC block layout | Yes | — |

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

## repack

Rewrites a Unity Archive into a new archive that contains the same files, compressed with a block layout you choose. The original file is not modified.

Unity writes chunk-based (LZ4/LZ4HC) archives with blocks of up to 128 KB of uncompressed data, and there is no build option to change that. The archive format records the size and compression of every block, so other layouts can be read as well. `repack` makes it possible to measure what a different layout does to the download size, the patch size and the cost of reading the content, for example to tune it to a platform's storage. The input can use any compression that UnityDataTool can read, including LZMA; the output is uncompressed, LZ4 or LZ4HC (LZMA output is not supported).

The output keeps the archive format version and Unity version of the input, and by default it is checked after writing: both archives are read back through UnityFileSystemApi and every file must match the original byte for byte. If the check fails, the output is deleted.

### Quick Reference

```
UnityDataTool archive repack <archive-path> -o <output-file> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<archive-path>` | Path to the archive file | *(required)* |
| `-o, --output-file <path>` | Path of the repacked archive. Must differ from the input | *(required)* |
| `-c, --compression <None\|Lz4\|Lz4HC>` | Block compression | `Lz4HC` |
| `--block-size <bytes>` | Largest uncompressed size of a block (1 KB to 64 MB) | `131072` |
| `--max-compressed-block-size <bytes>` | Largest stored size of a block. A block that would compress to more is made shorter | *(no limit)* |
| `--split-at-file-boundaries` | Start a new block for each file, so that no block holds data from two files | *(blocks span files)* |
| `--file-alignment <bytes>` | Align the start of each file in the uncompressed data to a power of two, padding with zeros | `1` |
| `--block-alignment <bytes>` | Align the position of each block in the file to a power of two. Needs archive format version 9 (Unity 6.7 and later), where blocks record their own position | `1` |
| `--min-compression-ratio <ratio>` | Store a block uncompressed unless uncompressed size / compressed size reaches this ratio | `1.0` |
| `--skip-verify` | Do not read the output back to compare it with the input | |
| `-f, --format <Text\|Json>` | Output format of the report | `Text` |

A block is always stored uncompressed when compression does not make it smaller. A higher `--min-compression-ratio` extends that to data that only compresses a little (for example textures in a GPU compression format, or audio), which saves the decompression work when it is loaded.

### Example

```bash
UnityDataTool archive repack scenes.bundle -o scenes-512k.bundle --block-size 524288
UnityDataTool archive repack scenes.bundle -o scenes-split.bundle --split-at-file-boundaries -f Json
```

**Output:**
```
Repacked scenes into /path/to/scenes-split.bundle

                                      Original        Repacked
File Size                              362,352         364,873  (+0.7%)
Compression                              Lz4HC           Lz4HC
Block Count                                 11               9
Uncompressed Blocks                          0               0
Largest Block                          131,072         131,072
Read Amplification                       1.00x           1.00x

Verified: all 4 files match the original byte for byte.
```

The report compares the two layouts:

| Field | Meaning |
|-------|---------|
| Uncompressed Blocks | Blocks stored without compression in an archive that otherwise uses compression |
| Largest Block | Largest uncompressed block size, which is roughly the memory needed to decompress one block |
| Read Amplification | Uncompressed size of the blocks that must be read to load each file on its own, divided by the total size of the files. 1.00x means no block is shared between two files. A single LZMA block that holds four files reports 4.00x |

`archive info`, `archive blocks` and `archive list` work on the repacked file, and `dump` and `analyze` read it the same way as the original.

> **Note:** The output is checked with UnityFileSystemApi, which comes from the Unity Editor. Test a repacked archive in a Player build on your target platform before you ship it.

---

## Related: inspecting objects inside an archive

The `archive` command works at the container level — it shows the archive structure and can extract the raw files. To inspect the serialized objects inside those files, use the [`dump`](command-dump.md#archive-support) command, which can open an archive directly and dump all SerializedFiles inside it without extracting first.

| Command | Output | Use Case |
|---------|--------|----------|
| `archive extract` | Raw binary files (SerializedFiles, .resS, .resource, etc.) | When you need the individual files on disk |
| [`dump`](command-dump.md#archive-support) | Human-readable text representation of serialized objects | When you want to inspect object properties and values inside an archive |
