CREATE TABLE IF NOT EXISTS content_layout_loadable_objects
(
    -- The objects that can be loaded on demand, identified independently of the content file that
    -- holds them, plus where each came from in the source project.
    object_id_hash TEXT,             -- hash of GUID, LFID and identifier_type
    guid TEXT,                       -- AssetDatabase GUID of the source asset
    asset_path TEXT,
    lfid INTEGER,                    -- local file id of the object in the source asset
    identifier_type INTEGER,
    serialized_file_index INTEGER,   -- content_layout_serialized_files.file_index; NULL if dropped from the build
    output_lfid INTEGER,             -- local file id of the object in its output content file
    is_root_asset INTEGER,
    PRIMARY KEY (object_id_hash)
);
