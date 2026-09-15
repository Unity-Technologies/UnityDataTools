CREATE TABLE IF NOT EXISTS content_layout_serialized_files
(
    -- The content files (.cf) the ContentDirectory build produced, one row per entry in the
    -- layout's SerializedFiles array.
    file_index INTEGER,         -- the json array index; how the other content_layout tables name a file
    cfid TEXT,                  -- symbolic .cfid reference string (for built-ins: the built-in path)
    is_builtin INTEGER,
    content_hash TEXT,          -- NULL for built-ins; the filename is content_hash || '.cf'
    serialized_file INTEGER,    -- serialized_files.id; NULL for built-ins and for a layout-only analyze
    PRIMARY KEY (file_index)
);
