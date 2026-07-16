-- The objects that can be loaded on demand (LoadableObjectIds in the json), identified
-- independently of the serialized file that contains them. Also records where each one came from
-- in the source project. The json's top-level RootAssets list is folded into the is_root_asset
-- flag. serialized_file_index is NULL when the object was dropped from the build (json value -1,
-- e.g. server build shader references).
CREATE TABLE IF NOT EXISTS content_layout_loadable_objects
(
    object_id_hash TEXT,             -- hash of GUID, LFID and identifier_type
    guid TEXT,                       -- AssetDatabase GUID of the source asset
    asset_path TEXT,
    lfid INTEGER,                    -- local file id of the object in the source asset
    identifier_type INTEGER,
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index, or NULL
    output_lfid INTEGER,             -- local file id of the object in its output serialized file
    is_root_asset INTEGER,
    PRIMARY KEY (object_id_hash)
);
