-- The scenes exposed as loadable in the build (LoadableSceneIds in the json).
CREATE TABLE IF NOT EXISTS content_layout_loadable_scenes
(
    guid TEXT,
    path TEXT,
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index, or NULL
    PRIMARY KEY (guid)
);
