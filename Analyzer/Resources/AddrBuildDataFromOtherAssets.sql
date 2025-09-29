CREATE TABLE IF NOT EXISTS addr_build_data_from_other_assets
(
    id INTEGER,
    build_id INTEGER,
    asset_guid TEXT,
    asset_path TEXT,
    file INTEGER,
    main_asset_type INTEGER,
    object_count INTEGER,
    serialized_size INTEGER,
    streamed_size INTEGER,
    PRIMARY KEY (id, build_id)
);