# UnityDataTool

A command-line tool for analyzing and inspecting Unity build output—AssetBundles, Player builds, Addressables, and more.

## Commands

| Command | Description | Documentation |
|---------|-------------|---------------|
| [`analyze`](#analyze) | Extract data from Unity files into a SQLite database | [Full docs →](Commands/analyze.md) |
| [`dump`](#dump) | Convert SerializedFiles to human-readable text | [Full docs →](Commands/dump.md) |
| [`archive`](#archive) | List or extract contents of Unity Archives | [Full docs →](Commands/archive.md) |
| [`find-refs`](#find-refs) | Trace reference chains to objects *(experimental)* | [Full docs →](Commands/find-refs.md) |

---

## Quick Start

```bash
# Show all commands
UnityDataTool --help

# Analyze AssetBundles into SQLite database
UnityDataTool analyze /path/to/bundles -o database.db

# Dump a file to text format
UnityDataTool dump /path/to/file.bundle -o /output/path

# Extract archive contents
UnityDataTool archive extract file.bundle -o contents/

# Find reference chains to an object
UnityDataTool find-refs database.db -n "ObjectName" -t "Texture2D"
```

Use `--help` with any command for details: `UnityDataTool analyze --help`

Use `--version` to print the tool version.

---

## analyze

Extract information from Unity Archives and SerializedFiles into a SQLite database.

```bash
UnityDataTool analyze <path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `-o, --output-file` | Output database filename | `database.db` |
| `-p, --search-pattern` | File filter pattern (`*.bundle`) | `*` |
| `-s, --skip-references` | Skip CRC/reference extraction (faster) | — |
| `-v, --verbose` | Show detailed progress | — |
| `--no-recurse` | Don't recurse into subdirectories | — |

**Example:**
```bash
UnityDataTool analyze /path/to/bundles -o my_database.db -p "*.bundle"
```

📖 [Full documentation](Commands/analyze.md) — Troubleshooting, database schema, example inputs

---

## dump

Convert SerializedFiles to human-readable text format.

```bash
UnityDataTool dump <path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `-o, --output-path` | Output folder | Current folder |
| `-f, --output-format` | Output format | `text` |
| `-s, --skip-large-arrays` | Skip large array contents | — |
| `-i, --objectid` | Only dump this object ID | All objects |

**Example:**
```bash
UnityDataTool dump /path/to/file -o /output/path
```

📖 [Full documentation](Commands/dump.md) — Output format details, archive support

---

## archive

Work with Unity Archives (AssetBundles, `.data` files).

### list

```bash
UnityDataTool archive list <archive-path>
```

### extract

```bash
UnityDataTool archive extract <archive-path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `-o, --output-path` | Output directory | `archive` |

**Example:**
```bash
UnityDataTool archive extract scenes.bundle -o contents/
```

📖 [Full documentation](Commands/archive.md) — Sub-command details, comparison with dump

---

## find-refs

> ⚠️ Experimental

Find reference chains leading to specific objects. Requires a database from `analyze` (without `--skip-references`).

```bash
UnityDataTool find-refs <database> [options]
```

| Option | Description |
|--------|-------------|
| `-i, --object-id` | Object ID to trace |
| `-n, --object-name` | Object name to trace |
| `-t, --object-type` | Type filter (with `-n`) |
| `-o, --output-file` | Output filename |
| `-a, --find-all` | Find all chains (slower) |

**Example:**
```bash
UnityDataTool find-refs my_database.db -n "MyTexture" -t "Texture2D" -o refs.txt
```

📖 [Full documentation](Commands/find-refs.md) — Use cases, output format

---

## Installation

### Building

First, build the solution as described in the [main README](../README.md#how-to-build).

The executable will be at:
```
UnityDataTool/bin/Release/net9.0/UnityDataTool.exe
```

> **Tip:** Add the directory containing `UnityDataTool.exe` to your `PATH` environment variable for easy access.

### Mac Instructions

On Mac, publish the project to get an executable:

**Intel Mac:**
```bash
dotnet publish UnityDataTool -c Release -r osx-x64 -p:PublishSingleFile=true -p:UseAppHost=true
```

**Apple Silicon Mac:**
```bash
dotnet publish UnityDataTool -c Release -r osx-arm64 -p:PublishSingleFile=true -p:UseAppHost=true
```

If you see a warning about `UnityFileSystemApi.dylib` not being verified, go to **System Preferences → Security & Privacy** and allow the file.

---

## Related Documentation

| Topic | Description |
|-------|-------------|
| [Analyzer Database Reference](../Analyzer/README.md) | SQLite schema, views, and extending the analyzer |
| [TextDumper Output Format](../TextDumper/README.md) | Understanding dump output |
| [ReferenceFinder Details](../ReferenceFinder/README.md) | Reference chain output format |
| [Analyze Examples](../Documentation/analyze-examples.md) | Practical database queries |
| [Comparing Builds](../Documentation/comparing-builds.md) | Strategies for build comparison |
| [Unity Content Format](../Documentation/unity-content-format.md) | TypeTrees and file formats |
