CREATE TABLE IF NOT EXISTS addressables_build_file_assets
(
    file_id INTEGER,
    build_id INTEGER,
    asset_rid INTEGER,
    PRIMARY KEY (file_id, build_id, asset_rid),
    FOREIGN KEY (file_id, build_id) REFERENCES addressables_build_files(id, build_id)
);