create table addr_build_schemas
(
    id INTEGER,
    build_id INTEGER,
    guid TEXT,
    type TEXT,
    PRIMARY KEY (id, build_id)
);