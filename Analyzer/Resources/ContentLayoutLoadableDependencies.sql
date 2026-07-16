-- Loadable objects referenced from each serialized file (LoadableDependencies in the json).
CREATE TABLE IF NOT EXISTS content_layout_loadable_dependencies
(
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index
    object_id_hash TEXT              -- references content_layout_loadable_objects.object_id_hash
);
