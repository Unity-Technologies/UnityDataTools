# analyze Command

The `analyze` command extracts information from Unity Archives (e.g. AssetBundles) and SerializedFiles and dumps the results into a SQLite database.

## Quick Reference

```
UnityDataTool analyze <paths>... [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<paths>...` | One or more files or directories to analyze. Directories are scanned; files are analyzed directly. | *(required)* |
| `-o, --output-file <file>` | Output database filename | `database.db` |
| `-p, --search-pattern <pattern>` | File search pattern applied when scanning directories (`*` and `?` supported) | `*` |
| `-s, --skip-references` | Do not extract references (smaller DB, no `refs` table). CRC is still computed. | `false` |
| `--skip-crc` | Skip the CRC32 checksum calculation (faster; `objects.crc32` will be 0) | `false` |
| `-v, --verbose` | Show more information during analysis | `false` |
| `--no-recurse` | Do not recurse into sub-directories when scanning directories | `false` |
| `-d, --typetree-data <file>` | Load an external TypeTree data file before processing (Unity 6.5+) | — |

There is no way to append to an existing database, so every file you want in the results must be
included in a single `analyze` invocation. Pass multiple paths to combine files from more than one
location into the same database.

## Examples

Analyze all files in a directory:
```bash
UnityDataTool analyze /path/to/asset/bundles
```

Analyze a single file (no need for `.` plus `-p`):
```bash
UnityDataTool analyze /path/to/asset/bundles/my.bundle
```

Combine a build output directory with a build report file kept in a separate location:
```bash
UnityDataTool analyze /path/to/build/output /path/to/Library/LastBuild.buildreport
```

Analyze only `.bundle` files and specify a custom database name:
```bash
UnityDataTool analyze /path/to/asset/bundles -o my_database.db -p "*.bundle"
```

Fastest analysis (skip both reference extraction and CRC):
```bash
UnityDataTool analyze /path/to/bundles --skip-references --skip-crc
```

See also [Analyze Examples](../../Documentation/analyze-examples.md).

---

## What Can Be Analyzed

Each path may be an individual file or a directory. Directories are scanned (honoring
`--search-pattern` and `--no-recurse`); individually-named files are always analyzed. The analyze
command works with the following types of input:

| Input Type | Description |
|------------|-------------|
| **AssetBundle build output** | The output path of an AssetBundle build |
| **Addressables folder** | `StreamingAssets/aa` folder from a Player build, including BuildLayout files |
| **Player Data folder** | The `Data` folder of a Unity Player build |
| **Compressed Player builds** | The `data.unity3d` file will be analyzed like AssetBundles |
| **ContentDirectory build output** | The output of [`BuildPipeline.BuildContentDirectory`](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/BuildPipeline.BuildContentDirectory.html) (Unity 6.6+), ideally together with its build history folder. See [ContentDirectory builds](#contentdirectory-builds) |
| **ContentLayout.json** | The layout file of a ContentDirectory build, imported into dedicated tables. Also useful on its own for querying a large layout with SQL. See [ContentLayout in the Analyze Database](contentlayout-database.md) |
| **BuildReport files** | The build report is typically found inside `Library/BuildHistory` or at `Library/LastBuild.buildreport`.  It is a binary serialized file |
| **Entities content** | `StreamingAssets/ContentArchives` folder for [Entities](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/content-management-intro.html) projects |
| **AssetDatabase Artifacts** | The tool will work to some extent with serialized files created in the AssetDatabase artifact storage, inside the Library folder |

> **Note**: Some platforms require extracting content from platform-specific containers first (e.g., `.apk` files on Android).

---

## ContentDirectory builds

Analyze a ContentDirectory build together with its `ContentLayout.json` by passing the build output
folder and the build report folder (or the layout file itself) in one call:

```bash
UnityDataTool analyze /path/to/ContentDirectory /path/to/Library/BuildHistory/<build-directory> -o database.db
```

The layout provides the source-asset mapping (imported as the `content_layout` tables, see
[ContentLayout in the Analyze Database](contentlayout-database.md)) and the dependency information
that resolves the references between the build's Content Files (see
[Content Directory Format](contentdirectory-format.md)). Without it, cross-file references cannot be
resolved and are recorded in `dangling_refs`.

A `ContentLayout.json` is only used when its `BuildManifestHash` matches the analyzed build's
`BuildManifestHash.txt` (a file guaranteed to exist in the build output folder, which analyze picks
up automatically). This exact-match rule prevents a stale or unrelated layout from silently
producing misleading results, and it means you can point analyze at a folder containing several
layouts (such as `Library/BuildHistory`) and the matching one is selected.

How the input combinations behave:

| Input | Result |
|-------|--------|
| ContentDirectory only | Analyzed, with a warning that the analysis is incomplete (broken references, no source mapping) |
| Subset of files from a ContentDirectory | Same as above; recognized by the `.cf` extension |
| ContentDirectory + matching ContentLayout.json | Analyzed with resolved references and source mapping |
| Subset of ContentDirectory files + ContentLayout.json | Works when a `BuildManifestHash.txt` can be found for the hash match; error when the layout cannot be validated |
| ContentDirectory + multiple ContentLayout.json | The one matching the build is used, the rest are ignored; error when none match |
| ContentLayout.json only | Imported on its own (layout tables only) |
| Multiple ContentDirectory builds | Error — analyze a single build at a time |
| Non-ContentDirectory content + ContentDirectory | Allowed, e.g. a Player build and a ContentDirectory together for duplicate detection |

---

## Output Database

The analysis creates a SQLite database that can be explored using tools like [DB Browser for SQLite](https://sqlitebrowser.org/) or the command line `sqlite3` tool.

**Refer to the [Analyzer documentation](analyzer.md) for the database schema reference and information about extending this command.**

---

## Troubleshooting

### File Loading Warnings

```
Failed to load 'C:\....\MyData.db'. File may be corrupted or was serialized with a newer version of Unity.
```

These warnings occur when the tool encounters non-Unity files in the analyzed directory. They are usually harmless—the analyze process continues and produces a valid database.

**Solutions:**
- Use `-p "*.bundle"` to filter by file extension
- Use `--no-recurse` to limit directory depth
- Use `-v` (verbose) to see which files are ignored

The tool automatically ignores common non-Unity file types (`.txt`, `.json`, `.manifest`, etc.).

### TypeTree Errors

```
Error processing file: C:\...\TestProject_Data\level0
System.ArgumentException: Invalid object id.
```

This error occurs when SerializedFiles are built without TypeTrees. The command will skip these files and continue.

**Solutions:**
- Enable **ForceAlwaysWriteTypeTrees** in your Unity build settings. See [Unity Content Format](../../Documentation/unity-content-format.md) for details.
- If your bundles were built with external TypeTree data (Unity 6.5+), use the `--typetree-data` option to load the TypeTree data file before analysis:

```bash
UnityDataTool analyze /path/to/bundles --typetree-data /path/to/typetree.bin
```

### SQL Constraint Errors

```
SQLite Error 19: 'UNIQUE constraint failed: objects.id'
```
or
```
SQLite Error 19: 'UNIQUE constraint failed: serialized_files.id'.
```

These errors occur when the same serialized file name appears in multiple sources:

| Cause | Solution |
|-------|----------|
| Multiple builds in same directory | Analyze each build separately |
| Scenes with same filename (different paths) | Rename scenes to be unique |
| AssetBundle variants | Analyze variants separately |

See [Comparing Builds](../../Documentation/comparing-builds.md) for strategies to compare different versions of builds.

### Slow Analyze times, large output database

Two independent flags reduce analyze time and database size:

* `--skip-crc` skips the CRC32 calculation. This is usually the largest time saver, because computing a CRC requires reading the full content of every object, including large texture, mesh and audio data in companion `.resS`/`.resource` files.
* `--skip-references` skips reference extraction, which is the largest contributor to database size (the `refs` table). The references are not needed for core asset inventory and size information.

For the fastest, smallest result, combine them.

A real life analyze of a big Addressables build, skipping both references and CRC, shows how large a difference this can make:

* 208 seconds and produced a 500MB database (default)
* 9 seconds and produced a 68 MB file (with `--skip-references --skip-crc`)

When `--skip-references` is used, some functionality is lost:

* the `find-refs` command will not work
* `view_material_shader_refs` and `view_material_texture_refs` will be empty
* `script_object_view` will be empty
* `dangling_refs` will be empty
* Queries that look at the relationship between objects will not work.  For example the refs table is required to link between a `MonoBehaviour` and its `MonoScript`.

When `--skip-crc` is used, the `objects.crc32` column will be 0 for all objects. This means:

* No detection of identical objects by content hash (See [Comparing Builds](../../Documentation/comparing-builds.md))
* The `view_potential_duplicates` view relies partially on CRC32 to distinguish true duplicates

