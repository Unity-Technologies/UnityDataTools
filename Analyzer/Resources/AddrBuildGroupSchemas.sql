CREATE TABLE IF NOT EXISTS addr_build_group_schemas
(
    group_id INTEGER,
    build_id INTEGER,
    schema_rid INTEGER,
    PRIMARY KEY (group_id, build_id, schema_rid),
    FOREIGN KEY (group_id, build_id) REFERENCES addr_build_groups(id, build_id)
);