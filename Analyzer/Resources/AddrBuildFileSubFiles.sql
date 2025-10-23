CREATE TABLE IF NOT EXISTS addressables_build_file_sub_files
(
    file_id INTEGER,
    build_id INTEGER,
    sub_file_rid INTEGER,
    PRIMARY KEY (file_id, build_id, sub_file_rid),
    FOREIGN KEY (file_id, build_id) REFERENCES addressables_build_files(id, build_id)
);