CREATE TABLE IF NOT EXISTS addressables_build_sub_files
(
    id INTEGER,
    build_id INTEGER,
    is_serialized_file INTEGER,
    name TEXT,
    size INTEGER,
    PRIMARY KEY (id, build_id)
);