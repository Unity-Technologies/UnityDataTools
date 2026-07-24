# Using UnityDataTool with an AI Agent

AI agents (Claude Code, Codex, Cursor, and similar tools) are good at answering questions about a
Unity build when they are pointed at UnityDataTool: the `analyze` command turns the build output into
a SQLite database that an agent can query directly, and the other commands let it drill into
individual files and objects. This page is the recommended workflow, plus the handful of facts that
are not obvious from `--help` and tend to cost the most discovery time. It is written so it can be
pasted (or linked) into an agent's context, and it is just as useful for humans writing scripts.

## The core loop

1. **Analyze the build output into a database.** One build per database (see below).

   ```
   UnityDataTool analyze /path/to/build -o Analysis.db
   ```

2. **Query the database.** Any SQLite client works. The `sqlite3` command-line shell is the most
   convenient; if it is not installed, Python's standard library works without any extra setup:

   ```
   sqlite3 Analysis.db ".mode column" "SELECT * FROM view_breakdown_by_type LIMIT 15;"
   ```

   ```
   python -c "import sqlite3; [print(*r) for r in sqlite3.connect('Analysis.db').execute(\"SELECT type, count, pretty_size FROM view_breakdown_by_type LIMIT 15\")]"
   ```

3. **Start from the views, not the raw tables.** The database ships with views that join the
   underlying tables into directly useful shapes. `object_view` (one row per object, with resolved
   type/archive/file names and `pretty_size`) and `view_breakdown_by_type` (count and total size per
   object type) answer most first questions. List everything that is available with:

   ```
   sqlite3 Analysis.db "SELECT name FROM sqlite_master WHERE type = 'view' ORDER BY name;"
   ```

   The full schema is documented in the [Analyzer database reference](analyzer.md); worked example
   queries are in [Example usage of Analyze](analyze-examples.md).

4. **Drill down into specific files and objects.** Once a query has identified something
   interesting, the other commands show the actual content:
   * [`dump`](command-dump.md) prints an object's full serialized properties as text.
   * [`serialized-file`](command-serialized-file.md) inspects a SerializedFile's header, object
     list, and external references.
   * [`archive`](command-archive.md) lists or extracts the contents of an AssetBundle or other
     Unity archive.

5. **Trace why something is in the build** with [`find-refs`](command-find-refs.md), which walks the
   reference graph recorded in the database.

## Facts that save time

**Two different ids.** `object_view` (and `objects`) has both an `id` and an `object_id` column, and
the two drill-down commands take different ones:

* `id` is a small sequential row number assigned by `analyze`, unique across the whole database.
  `find-refs -i` takes this one.
* `object_id` is the object's serialized local file id (the `m_PathID` seen in references), a signed
  64-bit value that is only unique within its SerializedFile. `dump -i` takes this one.

**One build per database.** `analyze` refuses input where two archives (or two standalone
SerializedFiles) have the same name, because queries would be ambiguous — this happens when a
directory contains several builds, or the same bundles built for multiple targets. Analyze each
build into its own database and query them separately ([Comparing Builds](comparing-builds.md)
shows patterns for diffing them).

**TypeTrees are required.** Object contents can only be interpreted when the files contain TypeTree
metadata. AssetBundles include it by default; Player builds do not, so analyzing a Player build
typically reports `Files without TypeTrees` and those files contribute nothing to the database (a
run where every file is skipped produces a valid but empty database). To analyze Player data, build
with the `ForceAlwaysWriteTypeTrees` diagnostic switch — see
[Player Build Format](playerbuild-format.md).

**References live in the `refs` table.** Each row records that one object references another:
`object` is the referencing object's `id`, `referenced_object` is the referenced object's `id`, and
`refs_view` adds the property path and type as strings. This is the raw data behind `find-refs`,
and it is often quicker to query it directly, e.g. "what references object 140":

```
sqlite3 Analysis.db "SELECT object, property_path FROM refs_view WHERE referenced_object = 140;"
```

**Empty names are normal.** Many object types (Transform, GameObject components in general) have no
`name`; `object_view.game_object` links components back to their named GameObject.

## Worked example: diagnose a bundle

Given the question "what is in this AssetBundle and does anything look wasteful?", this sequence
answers it. Analyze just the one file into a throwaway database:

```
UnityDataTool analyze /path/to/bundles -o bundle.db -p my.bundle
```

Then look at, in order:

```sql
-- What is in it, by type
SELECT * FROM view_breakdown_by_type;

-- The biggest individual objects
SELECT object_id, type, name, pretty_size FROM object_view ORDER BY size DESC LIMIT 20;

-- Texture formats, sizes, and Read/Write flags (rw doubles runtime memory when enabled)
SELECT name, format, width, height, mip_count, rw_enabled, pretty_size FROM texture_view;

-- Meshes with Read/Write enabled
SELECT name, vertices, rw_enabled, pretty_size FROM mesh_view;

-- Objects that appear more than once (same name/type/size in different files)
SELECT * FROM view_potential_duplicates;
```

Interpretation notes: `rw_enabled = 1` on textures and meshes doubles their runtime memory cost and
is only needed when scripts access the data on the CPU. Rows in `view_potential_duplicates` that
span archives usually mean a shared dependency was not assigned to a common bundle — expected for
independent builds, actionable within one build. To see a suspicious object in full, dump it:

```
UnityDataTool dump /path/to/bundles/my.bundle -i <object_id> --stdout
```

## Providing schema context to a chat-based AI

When using a chat AI that cannot run commands itself, the same information has to be provided as
context. Dump the schema of your database into a text file and attach it before asking for queries:

```
sqlite3 Analysis.db ".schema" > schema_dump.sql.txt
```

Note that the produced database's schema shows bare table definitions; the meaning of the columns is
documented in the [Analyzer database reference](analyzer.md), which is also useful context.

## Related documentation

| Topic | Description |
|-------|-------------|
| [Command-line tool](unitydatatool.md) | All commands and their options |
| [Analyzer database reference](analyzer.md) | Tables, views, and their columns |
| [Example usage of Analyze](analyze-examples.md) | More worked queries |
| [Comparing builds](comparing-builds.md) | Finding what changed between two builds |
| [Overview of Unity Content](unity-content-format.md) | SerializedFiles, Archives, and TypeTrees |
