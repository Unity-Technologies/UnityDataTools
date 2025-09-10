create table addr_build_explicit_assets
(
    id INTEGER,
    build_id INTEGER,
    bundle INTEGER,
    file INTEGER,
    asset_hash TEXT,
    asset_path TEXT,
    addressable_name TEXT,
    group_guid TEXT,
    guid TEXT,
    internal_id TEXT,
    main_asset_type INTEGER,
    serialized_size INTEGER,
    streamed_size INTEGER,
    PRIMARY KEY (id, build_id)
);