CREATE TABLE IF NOT EXISTS content_layout_loadable_objects
(
    -- The objects that can be loaded on demand, identified independently of the content file that
    -- holds them, plus where each came from in the source project. This is the variant created when
    -- a version 2 layout (Unity 6.6) is imported; it adds source columns that newer layout versions
    -- no longer record.
    loadable_index INTEGER,          -- the json array index; how the other content_layout tables name a loadable
    guid TEXT,                       -- AssetDatabase GUID of the source asset
    lfid INTEGER,                    -- local file id of the object in its output content file
    identifier_type INTEGER,
    serialized_file_index INTEGER,   -- content_layout_serialized_files.file_index; NULL if dropped from the build
    is_root_asset INTEGER,           -- 1-based position in the layout's RootAssets; 0 when not a root asset
    asset_path TEXT,                 -- v2 layouts only: path of the source asset
    source_lfid INTEGER,             -- v2 layouts only: local file id of the object in the source asset
    PRIMARY KEY (loadable_index)
);
