CREATE TABLE IF NOT EXISTS content_layout_source_assets
(
    -- The source assets built into each content file. The same asset path can appear in more than
    -- one file, so this is many-to-many.
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index
    asset_path TEXT
);
