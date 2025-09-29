CREATE TABLE IF NOT EXISTS addr_build_explicit_asset_labels
(
    explicit_asset_id INTEGER,
    build_id INTEGER,
    label TEXT,
    PRIMARY KEY (explicit_asset_id, build_id, label),
    FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addr_build_explicit_assets(id, build_id)
);