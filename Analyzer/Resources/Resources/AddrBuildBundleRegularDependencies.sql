create table addr_build_bundle_regular_dependencies
(
    bundle_id INTEGER,
    build_id INTEGER,
    dependency_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, dependency_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
);