# analyze Command

The `analyze` command extracts information from Unity Archives (e.g. AssetBundles) and SerializedFiles and dumps the results into a SQLite database.

## Quick Reference

```
UnityDataTool analyze <path> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<path>` | Path to folder containing files to analyze | *(required)* |
| `-o, --output-file <file>` | Output database filename | `database.db` |
| `-p, --search-pattern <pattern>` | File search pattern (`*` and `?` supported) | `*` |
| `-s, --skip-references` | Skip CRC and reference extraction (faster, smaller DB) | `false` |
| `-v, --verbose` | Show more information during analysis | `false` |
| `--no-recurse` | Do not recurse into sub-directories | `false` |

## Examples

Analyze all files in a directory:
```bash
UnityDataTool analyze /path/to/asset/bundles
```

Analyze only `.bundle` files and save to a custom database:
```bash
UnityDataTool analyze /path/to/asset/bundles -o my_database.db -p "*.bundle"
```

Fast analysis (skip reference tracking):
```bash
UnityDataTool analyze /path/to/bundles -s
```

---

## What Can Be Analyzed

The analyze command works with the following types of directories:

| Input Type | Description |
|------------|-------------|
| **AssetBundle build output** | The output path of an AssetBundle build |
| **Addressables folder** | `StreamingAssets/aa` folder from a Player build |
| **Entities content** | `StreamingAssets/ContentArchives` folder for [Entities](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/content-management-intro.html) projects |
| **Player Data folder** | The `Data` folder of a Unity Player build |
| **Compressed Player builds** | The `data.unity3d` file will be analyzed like AssetBundles |

> **Note**: Some platforms require extracting content from platform-specific containers first (e.g., `.apk` files on Android).

---

## Output Database

The analysis creates a SQLite database that can be explored using tools like [DB Browser for SQLite](https://sqlitebrowser.org/) or the command line `sqlite3` tool.

**Refer to the [Analyzer documentation](../../Analyzer/README.md) for complete database schema reference and usage examples.**

See also: [Analyze Examples](../../Documentation/analyze-examples.md)

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

**Solution:** Enable **ForceAlwaysWriteTypeTrees** in your Unity build settings. See [Unity Content Format](../../Documentation/unity-content-format.md) for details.

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

