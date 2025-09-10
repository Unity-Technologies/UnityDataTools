create table addr_build_bundle_dependent_bundles
(
    bundle_id INTEGER,
    build_id INTEGER,
    dependent_bundle_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, dependent_bundle_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
);