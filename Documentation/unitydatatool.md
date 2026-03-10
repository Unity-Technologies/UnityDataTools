# UnityDataTool

A command-line tool for analyzing and inspecting Unity build output—AssetBundles, Player builds, Addressables, and more.

## Commands

| Command | Description |
|---------|-------------|
| [`analyze`](command-analyze.md) | Extract data from Unity files into a SQLite database |
| [`dump`](command-dump.md) | Convert SerializedFiles to human-readable text |
| [`archive`](command-archive.md) | List or extract contents of Unity Archives |
| [`serialized-file`](command-serialized-file.md) | Quick inspection of SerializedFile metadata |
| [`find-refs`](command-find-refs.md) | Trace reference chains to objects *(experimental)* |

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

# Quick inspect SerializedFile
UnityDataTool serialized-file objectlist level0
UnityDataTool sf externalrefs sharedassets0.assets --format json

# Find reference chains to an object
UnityDataTool find-refs database.db -n "ObjectName" -t "Texture2D"
```

Use `--help` with any command for details: `UnityDataTool analyze --help`

Use `--version` to print the tool version.

## External TypeTree Data

Starting with Unity 6.5, asset bundles can be built with TypeTree data extracted into a separate file. When bundles are built this way, the TypeTree data file must be loaded before the bundles can be processed.

The `--typetree-data` (`-d`) option is available on the [`analyze`](command-analyze.md) and [`dump`](command-dump.md) commands:

```bash
# Analyze bundles that use an external TypeTree data file
UnityDataTool analyze /path/to/bundles --typetree-data /path/to/typetree.bin

# Dump a bundle with external TypeTree data
UnityDataTool dump /path/to/file.bundle -d /path/to/typetree.bin
```

> **Note:** This option requires a version of UnityFileSystemApi from Unity 6.5 or newer. Using it with an older version of the library will produce an error message.


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
| [Analyzer Database Reference](analyzer.md) | SQLite schema, views, and extending the analyzer |
| [TextDumper Output Format](textdumper.md) | Understanding dump output |
| [ReferenceFinder Details](referencefinder.md) | Reference chain output format |
| [Analyze Examples](analyze-examples.md) | Practical database queries |
| [Comparing Builds](comparing-builds.md) | Strategies for build comparison |
| [Unity Content Format](unity-content-format.md) | TypeTrees and file formats |
