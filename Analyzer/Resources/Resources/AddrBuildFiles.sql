create table addr_build_files
(
    id INTEGER,
    build_id INTEGER,
    bundle INTEGER,
    bundle_object_info_size INTEGER,
    mono_script_count INTEGER,
    mono_script_size INTEGER,
    name TEXT,
    preload_info_size INTEGER,
    write_result_filename TEXT,
    PRIMARY KEY (id, build_id)
);