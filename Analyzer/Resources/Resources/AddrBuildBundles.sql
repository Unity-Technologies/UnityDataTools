create table addr_build_bundles
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