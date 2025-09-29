CREATE TABLE IF NOT EXISTS addr_build_bundle_files
(
    bundle_id INTEGER,
    build_id INTEGER,
    file_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, file_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
);