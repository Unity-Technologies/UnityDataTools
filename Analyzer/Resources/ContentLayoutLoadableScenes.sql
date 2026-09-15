CREATE TABLE IF NOT EXISTS content_layout_loadable_scenes
(
    -- The scenes exposed as loadable in the build.
    guid TEXT,
    path TEXT,
    serialized_file_index INTEGER,   -- content_layout_serialized_files.file_index; NULL if dropped from the build
    PRIMARY KEY (guid)
);
