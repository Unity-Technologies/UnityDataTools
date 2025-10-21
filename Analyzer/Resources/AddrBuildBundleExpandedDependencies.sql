CREATE TABLE IF NOT EXISTS addressables_build_bundle_expanded_dependencies
(
    bundle_id INTEGER,
    build_id INTEGER,
    dependency_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, dependency_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addressables_build_bundles(id, build_id)
);