# AGENTS.md

This file provides guidance to AI agents when working with code in this repository.

> Using the tool rather than working on it? To analyze a Unity build with UnityDataTool, see
> [Using UnityDataTool with an AI Agent](Documentation/agent-guide.md).

## Project Overview

UnityDataTools is a .NET 9.0 command-line tool for analyzing Unity build output (AssetBundles, Player builds, Addressables). It extracts data from Unity's proprietary binary formats into SQLite databases and human-readable text files. The tool showcases the UnityFileSystemApi native library and serves as both a production tool and reference implementation.

## Common Commands

### Building
```bash
# Build entire solution in Release mode
dotnet build -c Release

# Build from solution file
dotnet build UnityDataTools.sln -c Release

# Build specific project
dotnet build UnityDataTool/UnityDataTool.csproj -c Release
```

Output location (Windows): `UnityDataTool\bin\Release\net9.0\UnityDataTool.exe`

### Publishing (Mac-specific)
```bash
# Intel Mac
dotnet publish UnityDataTool -c Release -r osx-x64 -p:PublishSingleFile=true -p:UseAppHost=true

# Apple Silicon Mac
dotnet publish UnityDataTool -c Release -r osx-arm64 -p:PublishSingleFile=true -p:UseAppHost=true
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test UnityFileSystem.Tests/UnityFileSystem.Tests.csproj
dotnet test Analyzer.Tests/Analyzer.Tests.csproj
dotnet test UnityDataTool.Tests/UnityDataTool.Tests.csproj

# Run tests with filter
dotnet test --filter "FullyQualifiedName~SerializedFile"
```

Test projects: UnityFileSystem.Tests, Analyzer.Tests, UnityDataTool.Tests, TestCommon (helper library)

### Code Style

#### Comments

* Write comments that explain "why".  A few high level comments explaining the purpose of classes or methods is very helpful.  Comments explaining tricky code are also helpful.
* Avoid comments that are redundant with the code.  Do not comment before each line of code explaining what it does unless there is something that is not obvious going on.
* Do not use formal C# XML format when commenting methods, unless it is in an important interface class like UnityFileSystem.

#### Formatting

To repair white space or style issues, run:

```
dotnet format whitespace . --folder
dotnet format style
```

### Running the Tool
```bash
# Show all commands
UnityDataTool --help

# Analyze AssetBundles into SQLite database
UnityDataTool analyze /path/to/bundles -o database.db

# Dump binary file to text format
UnityDataTool dump /path/to/file.bundle -o /output/path

# Extract archive contents
UnityDataTool archive extract file.bundle -o contents/

# Quick inspect SerializedFile metadata
UnityDataTool serialized-file objectlist level0
UnityDataTool sf externalrefs sharedassets0.assets --format json

# Find reference chains to an object
UnityDataTool find-refs database.db -n "ObjectName" -t "Texture2D"
```

## Architecture

### Component Hierarchy
```
UnityDataTool (CLI executable)  — wires the feature libraries into commands
├── Analyzer → SQLite database generation
├── TextDumper → Human-readable text output
├── ReferenceFinder → Object reference chain tracing (queries the Analyzer database)
├── Archive → inspect / extract Unity Archives (the `archive` command)
├── SerializedFile → inspect SerializedFile header/metadata/objects (the `serialized-file` command)
└── (base libraries, used by the feature libraries above)
    ├── UnityBinaryFormat → C# parsers & helpers for Archives / SerializedFiles
    ├── UnityDataModels → schemas for JSON build-report formats
    └── UnityFileSystem → C# wrapper for native library
        └── UnityFileSystemApi (native .dll/.dylib/.so)
```

The feature libraries each build on UnityBinaryFormat and UnityFileSystem. See the
"Repository content" section of `README.md` for a diagram of the dependencies.

### Key Architectural Patterns

**Native Interop**: UnityFileSystem wraps UnityFileSystemApi (native library from Unity Editor) via P/Invoke in `DllWrapper.cs`. The native library reads Unity Archive and SerializedFile formats.

**TypeTree Navigation**: Unity binary files contain TypeTrees that describe object serialization. The `RandomAccessReader` class navigates these trees like property accessors: `reader["m_Name"].GetValue<string>()`. This enables the tool to interpret objects without hardcoded type knowledge.


#### Analyze Architecture Patterns

**Parser Pattern**: `ISQLiteFileParser` interface allows multiple parsers to handle different file formats:
- `SerializedFileParser` - Unity binary files (AssetBundles, Player data)
- `AddressablesBuildLayoutParser` - JSON build reports

**Core Data**: Tables are populated with the core information about analyzed serialized files, e.g. objects and serialized_files.

**Handlers**: Type-specific handlers extract specialized properties for Unity object types and populate additional tables. For example Mesh, AnimationClip, Shader, BuildReport, MonoScript.

**Views**: The database schema includes convenient views for seeing the data in useful ways, e.g. `object_view`. See `Documentation/analyzer.md` and `Documentation/addressables-build-reports.md` for schema details.

CLI entry point is `UnityDataTool/Program.cs` using System.CommandLine. Per-command documentation is in `Documentation/`.

## Extending UnityDataTools

### Extending Analyze

* New Unity types can be added by following the same pattern as the existing types, for example MonoScripts.
* Any database schema change (new or changed tables, views, or columns) must bump `PRAGMA user_version` in `Analyzer/Resources/Init.sql` and extend the version-history comment above it.
* Analysis of additional file formats could be added, for example AssetBundle manifest files by following the pattern of Addressables build layout files are handled.

### Skills

`Skills/` holds portable Claude Code skills (a `SKILL.md` plus any scripts they need) that wrap common analysis tasks. See `Skills/hybrid-duplication-audit/SKILL.md` for an example — it audits asset duplication across the AssetBundle/content-directory boundary in a hybrid Addressables build.

### Other Extensions

The UnityFileSystem API and UnityBinaryFormat parsing can be useful for other analysis.  The "dump", "analyze" and "serialized-file" commands can be considered reference examples of how to use those lower level tools.

For example:
* Showing file content in a GUI
* Populating a database with a different sqlite schema, for other types of analysis
* Producing reports in Json format.

## Important Concepts

### TypeTrees
TypeTrees describe how Unity objects are serialized (property names, types, offsets). They enable:
- Backward compatibility - reading files from different Unity versions
- Generic parsing without hardcoded type definitions
- Support for custom MonoBehaviours/ScriptableObjects

**Critical**: Player builds exclude TypeTrees by default for performance. To analyze Player data, enable the "ForceAlwaysWriteTypeTrees" diagnostic switch during build.

### File Formats
- **Unity Archive** - Container format (AssetBundles, .data files). Can be mounted as virtual filesystem.
- **SerializedFile** - Binary format storing Unity objects with TypeTree metadata.
- **JSON** - JSON files used for build reporting (schema found in UnityDataModels as C# data structures).

### Common Issues

**TypeTree Errors**: "Invalid object id" during analyze means SerializedFile lacks TypeTrees. Enable ForceAlwaysWriteTypeTrees or use files built with TypeTrees.

**SQL UNIQUE Constraint Errors**: Occurs when same SerializedFile name appears in multiple archives. This happens when trying to run analyzing on the output from multiple builds, or from AssetBundle variants. See `Documentation/comparing-builds.md` for solutions.

**Mac Security**: "UnityFileSystemApi.dylib cannot be opened" - Open System Preferences → Security & Privacy and allow the library.

## Native Library (UnityFileSystemApi)

The native library is included for Windows, Mac, and Linux in `UnityFileSystem/` directory. It's backward compatible and reads data files from most Unity versions.

To use a specific Unity version's library:
1. Find library in Unity Editor installation: `{UnityEditor}/Data/Tools/`
2. Copy to `UnityDataTool/UnityFileSystem/`:
   - Windows: `UnityFileSystemApi.dll`
   - Mac: `UnityFileSystemApi.dylib`
   - Linux: `UnityFileSystemApi.so`
3. Rebuild the tool

## Testing Data

* TestCommon/Data contains small reference files extracted from Unity builds (player and AssetBundles). These are used by the automated tests and also useful for manual testing.

* UnityProjects contains two Unity projects that generate test data for the test suites:
  * `Baseline` - tracks stable, broadly-used Unity versions and is upgraded only when necessary. Most UnityDataTools users inspect output from these versions. Its `TypeIdRegistryGenerator` regenerates `UnityFileSystem/TypeIdRegistry.cs`.
  * `LeadingEdge` - tracks the newest Unity version (currently the 6.6 beta) and is updated proactively so newer build features (Content Directory builds, serialized dictionaries, etc.) can be tested.
