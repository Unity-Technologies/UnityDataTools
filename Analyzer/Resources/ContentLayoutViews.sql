CREATE VIEW IF NOT EXISTS content_layout_serialized_files_view AS
-- One row per layout content file with its content hash, derived filename, artifact size and
-- core-table link. Built-in entries have no file on disk, so their path (stable_id) is shown as
-- the filename.
SELECT f.file_index, f.stable_id, f.is_builtin, f.artifact_index,
       CASE WHEN f.is_builtin = 1 THEN f.stable_id ELSE ba.content_hash || '.cf' END AS filename,
       ba.content_hash,
       ba.size,
       f.serialized_file,
       sf.archive
FROM content_layout_serialized_files f
LEFT JOIN content_layout_binary_artifacts ba ON ba.artifact_index = f.artifact_index
LEFT JOIN serialized_files sf ON sf.id = f.serialized_file;

CREATE VIEW IF NOT EXISTS content_layout_source_assets_view AS
-- Source asset to the content file(s) it was built into.
SELECT s.asset_path, f.file_index, f.filename, f.serialized_file
FROM content_layout_source_assets s
INNER JOIN content_layout_serialized_files_view f ON f.file_index = s.serialized_file_index;

CREATE VIEW IF NOT EXISTS content_layout_serialized_file_dependencies_view AS
-- File-to-file dependency edges with filenames resolved on both sides.
SELECT d.serialized_file_index, src.filename, d.position,
       d.dependency_index, dep.filename AS dependency_filename, dep.stable_id AS dependency_stable_id
FROM content_layout_serialized_file_dependencies d
INNER JOIN content_layout_serialized_files_view src ON src.file_index = d.serialized_file_index
INNER JOIN content_layout_serialized_files_view dep ON dep.file_index = d.dependency_index;

CREATE VIEW IF NOT EXISTS content_layout_binary_artifacts_view AS
-- Artifacts with their on-disk filename derived from the content hash and category.
SELECT artifact_index, content_hash, category, size,
       content_hash ||
       CASE category
           WHEN 'texture' THEN '.resS'   WHEN 'mesh' THEN '.resS'
           WHEN 'audio' THEN '.resource' WHEN 'video' THEN '.resource'
           WHEN 'contentfile' THEN '.cf' WHEN 'manifest' THEN '.json'
           ELSE '.' || category
       END AS filename
FROM content_layout_binary_artifacts;

CREATE VIEW IF NOT EXISTS content_layout_resource_files_view AS
-- The data files (.resS/.resource) each content file uses, derived from the artifact graph.
SELECT f.file_index, f.filename,
       ra.category, rav.filename AS data_filename, ra.size
FROM content_layout_serialized_files_view f
INNER JOIN content_layout_artifact_references r ON r.artifact_index = f.artifact_index
INNER JOIN content_layout_binary_artifacts ra ON ra.artifact_index = r.referenced_artifact_index
INNER JOIN content_layout_binary_artifacts_view rav ON rav.artifact_index = ra.artifact_index
WHERE ra.category IN ('texture', 'mesh', 'audio', 'video');
