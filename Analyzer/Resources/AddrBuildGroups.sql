CREATE TABLE IF NOT EXISTS addressables_build_groups
(
    id INTEGER,
    build_id INTEGER,
    guid TEXT,
    name TEXT,
    packing_mode TEXT,
    PRIMARY KEY (id, build_id)
);