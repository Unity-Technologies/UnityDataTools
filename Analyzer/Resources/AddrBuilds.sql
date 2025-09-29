CREATE TABLE IF NOT EXISTS addr_builds
(
    id INTEGER,
    name TEXT,
    build_target INTEGER,
    start_time TEXT,
    duration REAL,
    error TEXT,
    package_version TEXT,
    player_version TEXT,
    build_script TEXT,
    result_hash TEXT,
    type INTEGER,
    unity_version TEXT,
    PRIMARY KEY (id)
);
