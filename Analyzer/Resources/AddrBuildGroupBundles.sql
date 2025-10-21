CREATE TABLE IF NOT EXISTS addressables_build_group_bundles
(
    group_id INTEGER,
    build_id INTEGER,
    bundle_rid INTEGER,
    PRIMARY KEY (group_id, build_id, bundle_rid),
    FOREIGN KEY (group_id, build_id) REFERENCES addressables_build_groups(id, build_id)
);