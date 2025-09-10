create table addr_build_sub_files
(
    id INTEGER,
    build_id INTEGER,
    is_serialized_file INTEGER,
    name TEXT,
    size INTEGER,
    PRIMARY KEY (id, build_id)
);