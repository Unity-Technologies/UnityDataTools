# AGENTS.md

This file provides guidance to AI agents when working with code in this repository.

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

# Find reference chains to an object
UnityDataTool find-refs database.db -n "ObjectName" -t "Texture2D"
```

## Architecture

### Component Hierarchy
```
UnityDataTool (CLI executable)
├── Analyzer → SQLite database generation
├── TextDumper → Human-readable text output
├── ReferenceFinder → Object reference chain tracing
└── UnityFileSystem → C# wrapper for native library
    └── UnityFileSystemApi (native .dll/.dylib/.so)
```

### Key Architectural Patterns

**Native Interop**: UnityFileSystem wraps UnityFileSystemApi (native library from Unity Editor) via P/Invoke in `DllWrapper.cs`. The native library reads Unity Archive and SerializedFile formats.

**TypeTree Navigation**: Unity binary files contain TypeTrees that describe object serialization. The `RandomAccessReader` class navigates these trees like property accessors: `reader["m_Name"].GetValue<string>()`. This enables the tool to interpret objects without hardcoded type knowledge.

**Parser Pattern**: `ISQLiteFileParser` interface allows multiple parsers to handle different file formats:
- `SerializedFileParser` - Unity binary files (AssetBundles, Player data)
- `AddressablesBuildLayoutParser` - JSON build reports

**Handler Registry**: Type-specific handlers extract specialized properties for Unity object types. Handlers implement `ISQLiteHandler` and are registered in `SerializedFileSQLiteWriter.m_Handlers`:
- `MeshHandler` - vertices, indices, bones, blend shapes
- `Texture2DHandler` - width, height, format, mipmaps
- `ShaderHandler` - variants, keywords, subprograms
- `AudioClipHandler` - compression, channels, frequency
- `AnimationClipHandler` - legacy flag, events
- `AssetBundleHandler` - dependencies, preload data
- `PreloadDataHandler` - preloaded assets

**SQL Schema Resources**: Each handler has an embedded `.sql` resource file defining its tables and views (e.g., `Analyzer/SQLite/Resources/Mesh.sql`). Views join type-specific tables with the base `objects` table.

**Command Pattern**: SQL operations are encapsulated in classes derived from `AbstractCommand` with `CreateCommand()`, `SetValue()`, `ExecuteNonQuery()` methods.

### Data Flow (Analyze Command)

1. `Program.cs` → `HandleAnalyze()` → `AnalyzerTool.Analyze()`
2. AnalyzerTool finds files matching search pattern
3. For each file, parsers are tried in order (JSON first, then SerializedFile)
4. `SerializedFileParser.ProcessFile()`:
   - Checks for Unity Archive signature → calls `MountArchive()`
   - Otherwise treats as SerializedFile → calls `OpenSerializedFile()`
5. `SerializedFileSQLiteWriter.WriteSerializedFile()`:
   - Iterates through `sf.Objects`
   - Gets TypeTree via `sf.GetTypeTreeRoot(objectId)`
   - Creates `RandomAccessReader` to navigate properties
   - Looks up type-specific handler in `m_Handlers` dictionary
   - Handler extracts specialized properties (e.g., MeshHandler reads vertex count)
   - Writes to `objects` table + type-specific table (e.g., `meshes`)
   - Optionally processes PPtrs (references) and calculates CRC32
6. SQLiteWriter finalizes database with indexes and views

### Important Files

**Entry Points**:
- `UnityDataTool/Program.cs` - CLI using System.CommandLine
- `UnityDataTool/Commands/` - Command handlers (Analyze.cs, Dump.cs, Archive.cs, FindReferences.cs)

**Core Libraries**:
- `UnityFileSystem/UnityFileSystem.cs` - Init(), MountArchive(), OpenSerializedFile()
- `UnityFileSystem/DllWrapper.cs` - P/Invoke bindings to native library
- `UnityFileSystem/SerializedFile.cs` - Represents binary data files
- `UnityFileSystem/RandomAccessReader.cs` - TypeTree property navigation

**Analyzer**:
- `Analyzer/AnalyzerTool.cs` - Main API entry point
- `Analyzer/SQLite/SQLiteWriter.cs` - Base class for database writers
- `Analyzer/SQLite/Writers/SerializedFileSQLiteWriter.cs` - Handler registration
- `Analyzer/SQLite/Writers/AddressablesBuildLayoutSQLWriter.cs` - JSON report processing
- `Analyzer/SQLite/Handlers/` - Type-specific extractors
- `Analyzer/SerializedObjects/` - RandomAccessReader-based property readers
- `Analyzer/SQLite/Resources/` - SQL DDL schema files

**TextDumper**:
- `TextDumper/TextDumperTool.cs` - Converts binary to YAML-like text

**ReferenceFinder**:
- `ReferenceFinder/ReferenceFinderTool.cs` - Traces object dependency chains

## Extending the Tool

### Adding New Unity Type Support

1. Create handler class implementing `ISQLiteHandler`:
   ```
   Analyzer/SQLite/Handlers/FooHandler.cs
   ```

2. Create reader class using RandomAccessReader:
   ```
   Analyzer/SerializedObjects/Foo.cs
   ```

3. Register handler in `SerializedFileSQLiteWriter.cs`:
   ```csharp
   m_Handlers["Foo"] = new FooHandler();
   ```

4. Create SQL schema resource:
   ```
   Analyzer/SQLite/Resources/Foo.sql
   ```
   Define tables (e.g., `foos`) and views (e.g., `foo_view` joining `objects` and `foos`)

5. Reference the schema in handler's GetResourceName() method

### Adding New File Format Support

1. Create parser implementing `ISQLiteFileParser`
2. Create writer derived from `SQLiteWriter`
3. Add parser to `AnalyzerTool.parsers` list
4. Create SQL schema and Command classes as needed

Example: Addressables support uses `AddressablesBuildLayoutParser` + `AddressablesBuildLayoutSQLWriter` to parse JSON build reports.

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
- **Addressables BuildLayout** - JSON build report (buildlogreport.json, AddressablesReport.json)

### Database Views
The SQLite output uses views extensively to join base `objects` table with type-specific tables:
- `object_view` - All objects with basic properties
- `mesh_view` - Objects + mesh-specific columns
- `texture_view` - Objects + texture-specific columns
- `shader_view` - Objects + shader-specific columns
- `view_breakdown_by_type` - Aggregated size by type
- `view_potential_duplicates` - Assets included multiple times
- `asset_view` - Explicitly assigned assets only
- `shader_keyword_ratios` - Keyword variant analysis

See `Analyzer/README.md` and `Documentation/addressables-build-reports.md` for complete database schema documentation.

### Common Issues

**TypeTree Errors**: "Invalid object id" during analyze means SerializedFile lacks TypeTrees. Enable ForceAlwaysWriteTypeTrees or use files built with TypeTrees.

**File Loading Warnings**: "Failed to load... File may be corrupted" is normal for non-Unity files in analyzed directories. Use `-p` search pattern to filter (e.g., `-p "*.bundle"`).

**SQL UNIQUE Constraint Errors**: Occurs when same SerializedFile name appears in multiple archives. This happens when analyzing multiple builds in same directory or using AssetBundle variants. See `Documentation/comparing-builds.md` for solutions.

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

UnityFileSystemTestData is a Unity project that generates test data for the test suites. TestCommon provides shared test utilities.
