create table addr_build_explicit_asset_internal_referenced_other_assets
(
    explicit_asset_id INTEGER,
    build_id INTEGER,
    internal_referenced_other_asset_rid INTEGER,
    PRIMARY KEY (explicit_asset_id, build_id, internal_referenced_other_asset_rid),
    FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addr_build_explicit_assets(id, build_id)
);