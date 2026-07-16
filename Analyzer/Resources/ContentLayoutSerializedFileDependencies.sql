-- File-to-file dependency edges (SerializedFileDependencies in the json): the other serialized
-- files that must be loaded before this one. position preserves the array order, which is
-- significant: a PPtr's m_FileID inside the file resolves positionally through this list (see
-- Documentation/contentdirectory-format.md).
CREATE TABLE IF NOT EXISTS content_layout_serialized_file_dependencies
(
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index
    position INTEGER,                -- 1-based, matching the external reference table / m_FileID index
    dependency_index INTEGER,        -- references content_layout_serialized_files.file_index
    PRIMARY KEY (serialized_file_index, position)
);
