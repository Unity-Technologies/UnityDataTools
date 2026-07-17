-- Convenience views over the content_layout tables. Created together with the tables (only when
-- a ContentLayout.json is imported). See Documentation/contentlayout-database.md for the schema
-- reference.

-- One row per layout serialized file with the derived filename, artifact size, and core-table link.
-- Built-in entries have no file on disk, so their path (cfid) is shown as the filename.
CREATE VIEW IF NOT EXISTS content_layout_serialized_files_view AS
SELECT f.file_index, f.cfid, f.is_builtin,
       CASE WHEN f.is_builtin = 1 THEN f.cfid ELSE f.content_hash || '.cf' END AS filename,
       ba.size,
       f.serialized_file,
       sf.archive
FROM content_layout_serialized_files f
LEFT JOIN content_layout_binary_artifacts ba ON ba.content_hash = f.content_hash AND ba.category = 'contentfile'
LEFT JOIN serialized_files sf ON sf.id = f.serialized_file;

-- Source asset -> the file(s) it was built into. Built-in entries have no source assets, so no
-- filename fallback is needed here.
CREATE VIEW IF NOT EXISTS content_layout_source_assets_view AS
SELECT s.asset_path, f.file_index, f.content_hash || '.cf' AS filename, f.serialized_file
FROM content_layout_source_assets s
INNER JOIN content_layout_serialized_files f ON f.file_index = s.serialized_file_index;

-- File-to-file dependency edges with names resolved on both sides (via the files view, so
-- dependencies on built-in entries show their path instead of a NULL filename).
CREATE VIEW IF NOT EXISTS content_layout_serialized_file_dependencies_view AS
SELECT d.serialized_file_index, src.filename, d.position,
       d.dependency_index, dep.filename AS dependency_filename, dep.cfid AS dependency_cfid
FROM content_layout_serialized_file_dependencies d
INNER JOIN content_layout_serialized_files_view src ON src.file_index = d.serialized_file_index
INNER JOIN content_layout_serialized_files_view dep ON dep.file_index = d.dependency_index;

-- Loadables resolved to their analyzed object (object columns are NULL in a layout-only database).
CREATE VIEW IF NOT EXISTS content_layout_loadable_objects_view AS
SELECT l.object_id_hash, l.guid, l.asset_path, l.lfid, l.is_root_asset,
       f.content_hash || '.cf' AS filename,
       o.id AS object, t.name AS type, o.name, o.size
FROM content_layout_loadable_objects l
LEFT JOIN content_layout_serialized_files f ON f.file_index = l.serialized_file_index
LEFT JOIN objects o ON o.serialized_file = f.serialized_file AND o.object_id = l.output_lfid
LEFT JOIN types t ON t.id = o.type;

-- Artifacts with their derived on-disk filename (extension based on the category).
CREATE VIEW IF NOT EXISTS content_layout_binary_artifacts_view AS
SELECT artifact_index, content_hash, category, size,
       content_hash ||
       CASE category
           WHEN 'texture' THEN '.resS'   WHEN 'mesh' THEN '.resS'
           WHEN 'audio' THEN '.resource' WHEN 'video' THEN '.resource'
           WHEN 'contentfile' THEN '.cf' WHEN 'manifest' THEN '.json'
           ELSE '.' || category
       END AS filename
FROM content_layout_binary_artifacts;

-- The data files (.resS/.resource) each serialized file uses, derived from the artifact graph.
CREATE VIEW IF NOT EXISTS content_layout_resource_files_view AS
SELECT f.file_index, f.content_hash || '.cf' AS filename,
       ra.category, rav.filename AS data_filename, ra.size
FROM content_layout_serialized_files f
INNER JOIN content_layout_binary_artifacts ca ON ca.content_hash = f.content_hash AND ca.category = 'contentfile'
INNER JOIN content_layout_artifact_references r ON r.artifact_index = ca.artifact_index
INNER JOIN content_layout_binary_artifacts ra ON ra.artifact_index = r.referenced_artifact_index
INNER JOIN content_layout_binary_artifacts_view rav ON rav.artifact_index = ra.artifact_index
WHERE ra.category IN ('texture', 'mesh', 'audio', 'video');
