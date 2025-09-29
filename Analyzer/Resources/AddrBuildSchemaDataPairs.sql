CREATE TABLE IF NOT EXISTS addr_build_schema_data_pairs
(
    schema_id INTEGER,
    build_id INTEGER,
    key TEXT,
    value TEXT,
    PRIMARY KEY (schema_id, build_id, key),
    FOREIGN KEY (schema_id, build_id) REFERENCES addr_build_schemas(id, build_id)
);