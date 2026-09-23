CREATE TABLE IF NOT EXISTS content_layout_serialized_files
(
    -- The content files (.cf) the ContentDirectory build produced, one row per entry in the
    -- layout's SerializedFiles array.
    file_index INTEGER,         -- the json array index; how the other content_layout tables name a file
    stable_id TEXT,             -- identity hash used to reference this file (for built-ins: the built-in path)
    is_builtin INTEGER,
    artifact_index INTEGER,     -- content_layout_binary_artifacts.artifact_index; NULL for built-ins
    serialized_file INTEGER,    -- serialized_files.id; NULL for built-ins and for a layout-only analyze
    PRIMARY KEY (file_index)
);
