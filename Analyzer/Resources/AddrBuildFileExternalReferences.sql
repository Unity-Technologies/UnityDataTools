CREATE TABLE IF NOT EXISTS addr_build_file_external_references
(
    file_id INTEGER,
    build_id INTEGER,
    external_reference_rid INTEGER,
    PRIMARY KEY (file_id, build_id, external_reference_rid),
    FOREIGN KEY (file_id, build_id) REFERENCES addr_build_files(id, build_id)
);