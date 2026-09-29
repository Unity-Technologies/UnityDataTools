CREATE VIEW IF NOT EXISTS content_layout_loadable_objects_view AS
-- Loadables resolved to their analyzed object. The object columns are NULL in a layout-only
-- database. Selects the loadable table's columns as-is, so the v2-only columns (asset_path,
-- source_lfid) appear when the imported layout was version 2.
SELECT l.*,
       f.filename,
       o.id AS object, t.name AS type, o.name, o.size
FROM content_layout_loadable_objects l
LEFT JOIN content_layout_serialized_files_view f ON f.file_index = l.serialized_file_index
LEFT JOIN objects o ON o.serialized_file = f.serialized_file AND o.object_id = l.lfid
LEFT JOIN types t ON t.id = o.type;
