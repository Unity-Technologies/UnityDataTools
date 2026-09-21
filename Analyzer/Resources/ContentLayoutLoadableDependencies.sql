CREATE TABLE IF NOT EXISTS content_layout_loadable_dependencies
(
    -- The loadable objects each content file references.
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index
    loadable_index INTEGER           -- references content_layout_loadable_objects.loadable_index
);
