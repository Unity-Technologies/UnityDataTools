# Example queries for ContentDirectory builds

Example SQL queries against a database produced by running [`analyze`](command-analyze.md) on a
ContentDirectory build together with its `ContentLayout.json`. The tables and views used here are
documented in [ContentLayout in the Analyze Database](contentlayout-database.md); for general notes
on running queries (sqlite3 command line, DB Browser), see [Example usage of Analyze](analyze-examples.md).

Some of these queries need only the layout (they work in a layout-only database); others combine
the layout with the analyzed build content and need both on the analyze input.

## Which files was a source asset built into? (layout only)

The same source asset can contribute to more than one file:

```sql
SELECT * FROM content_layout_source_assets_view
WHERE asset_path = 'Assets/Textures/GreenStatic.png';
```

## Build size by artifact category (layout only)

```sql
SELECT category, COUNT(*) AS count, SUM(size) AS bytes
FROM content_layout_binary_artifacts
GROUP BY category ORDER BY bytes DESC;
```

## Everything required to load a source asset (layout only)

The recursive closure over the file dependencies: starting from the file(s) containing a source
asset, every serialized file that must be loaded with them.

```sql
WITH RECURSIVE closure(file_index) AS (
    SELECT serialized_file_index FROM content_layout_source_assets
    WHERE asset_path = 'Assets/ScriptableObjects/ContentDirectoryRoot.asset'
    UNION
    SELECT d.dependency_index
    FROM content_layout_serialized_file_dependencies d
    INNER JOIN closure c ON c.file_index = d.serialized_file_index
)
SELECT v.* FROM closure INNER JOIN content_layout_serialized_files_view v USING (file_index);
```

Add up the `size` column of the result for the total load footprint (excluding data loaded on
demand through loadables, and the `.resS`/`.resource` data files — join
`content_layout_data_files_view` to include those).

## All objects built from a source asset (layout + content)

Non-recursive: the objects inside the file(s) a given source asset was built into. For an asset
that produces many objects (an FBX, a scene) this shows what it expanded to.

```sql
SELECT o.*
FROM content_layout_source_assets s
INNER JOIN content_layout_serialized_files f ON f.file_index = s.serialized_file_index
INNER JOIN objects o ON o.serialized_file = f.serialized_file
WHERE s.asset_path = 'Assets/Scenes/Scene1.unity';
```

## The loadables and what they resolve to (layout + content)

Each loadable with the analyzed object it points at (type, name, size), and whether it is a root
asset of the build:

```sql
SELECT * FROM content_layout_loadable_objects_view ORDER BY is_root_asset DESC, asset_path;
```

## Data files of a serialized file (layout only)

The `.resS`/`.resource` files holding the streamed texture/mesh and audio/video data used by each
serialized file:

```sql
SELECT * FROM content_layout_data_files_view
WHERE filename = 'c0152db4dd710be51b2decb997325f34.cf';
```

## Checking reference resolution (layout + content)

When the layout was part of the analyze, the references between Content Files are resolved, so the
only expected entries in [`dangling_refs`](analyzer.md#dangling_refs--dangling_refs_view) are the
references into Unity's built-in resources:

```sql
SELECT DISTINCT sf.name FROM dangling_refs d
INNER JOIN serialized_files sf ON sf.id = d.serialized_file;
-- expected: 'unity default resources' only
```

Anything else listed here means part of the build was missing from the analyzed input.

## Related documentation

| Topic | Description |
|-------|-------------|
| [ContentLayout in the Analyze Database](contentlayout-database.md) | The tables and views these queries use. |
| [Example usage of Analyze](analyze-examples.md) | General analyze query examples and command-line usage. |
| [Content Directory Format](contentdirectory-format.md) | The build output format and its reference conventions. |
