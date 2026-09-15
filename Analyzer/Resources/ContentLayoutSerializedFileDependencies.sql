CREATE TABLE IF NOT EXISTS content_layout_serialized_file_dependencies
(
    -- File-to-file dependency edges: the other content files that must be loaded before this one.
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index
    position INTEGER,                -- 1-based; a PPtr's m_FileID resolves positionally through this list
    dependency_index INTEGER,        -- references content_layout_serialized_files.file_index
    PRIMARY KEY (serialized_file_index, position)
);
