create table addr_build_data_from_other_asset_referencing_assets
(
    data_from_other_asset_id INTEGER,
    build_id INTEGER,
    referencing_asset_rid INTEGER,
    PRIMARY KEY (data_from_other_asset_id, build_id, referencing_asset_rid),
    FOREIGN KEY (data_from_other_asset_id, build_id) REFERENCES addr_build_data_from_other_assets(id, build_id)
);