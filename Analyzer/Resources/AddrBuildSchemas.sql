CREATE TABLE IF NOT EXISTS addressables_build_schemas
(
    id INTEGER,
    build_id INTEGER,
    guid TEXT,
    type TEXT,
    PRIMARY KEY (id, build_id)
);