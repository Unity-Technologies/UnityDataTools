-- The source assets included in each serialized file (SourceAssets in the json). The same asset
-- path can appear in more than one file (e.g. an FBX split into multiple output files).
CREATE TABLE IF NOT EXISTS content_layout_source_assets
(
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index
    asset_path TEXT
);
