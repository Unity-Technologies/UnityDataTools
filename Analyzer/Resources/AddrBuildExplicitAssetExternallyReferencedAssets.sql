create table addr_build_explicit_asset_externally_referenced_assets
(
    explicit_asset_id INTEGER,
    build_id INTEGER,
    externally_referenced_asset_rid INTEGER,
    PRIMARY KEY (explicit_asset_id, build_id, externally_referenced_asset_rid),
    FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addr_build_explicit_assets(id, build_id)
);