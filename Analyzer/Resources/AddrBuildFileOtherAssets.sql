CREATE TABLE IF NOT EXISTS addressables_build_file_other_assets
(
    file_id INTEGER,
    build_id INTEGER,
    other_asset_rid INTEGER,
    PRIMARY KEY (file_id, build_id, other_asset_rid),
    FOREIGN KEY (file_id, build_id) REFERENCES addressables_build_files(id, build_id)
);