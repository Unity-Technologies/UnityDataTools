-- One row per entry in the layout's SerializedFiles array: the serialized files (.cf Content
-- Files) that the ContentDirectory build produced. file_index is the array index from the json
-- and is how the other content_layout tables reference a file.
-- serialized_file links to the core serialized_files table so layout data joins directly with
-- analyzed objects and references; it is NULL when the analyzed input did not include the build
-- content (e.g. a layout-only analyze) or for built-in entries.
CREATE TABLE IF NOT EXISTS content_layout_serialized_files
(
    file_index INTEGER,
    cfid TEXT,                  -- symbolic .cfid reference string (for built-ins: the built-in path)
    is_builtin INTEGER,
    content_hash TEXT,          -- NULL for built-ins; the filename is content_hash || '.cf'
    serialized_file INTEGER,    -- references serialized_files.id, or NULL
    PRIMARY KEY (file_index)
);
