CREATE TABLE IF NOT EXISTS addr_build_bundles
(
    id INTEGER,
    build_id INTEGER,
    asset_count INTEGER,
    build_status INTEGER,
    crc INTEGER,
    compression TEXT,
    dependency_file_size INTEGER,
    expanded_dependency_file_size INTEGER,
    file_size INTEGER,
    group_rid INTEGER,
    hash TEXT,
    internal_name TEXT,
    load_path TEXT,
    name TEXT,
    provider TEXT,
    result_type TEXT,
    PRIMARY KEY (id, build_id)
);

CREATE VIEW IF NOT EXISTS addr_build_cached_bundles AS SELECT build_id, concat(internal_name, '.bundle') AS cached_name, name AS catalog_name FROM addr_build_bundles;
