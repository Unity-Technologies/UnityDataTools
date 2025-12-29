CREATE TABLE IF NOT EXISTS build_reports(
    id INTEGER,
    build_type TEXT,
    build_result TEXT,
    platform_name TEXT,
    subtarget INTEGER,
    start_time TEXT,
    end_time TEXT,
    total_time_seconds INTEGER,
    total_size INTEGER,
    build_guid TEXT,
    total_errors INTEGER,
    total_warnings INTEGER,
    options INTEGER,
    asset_bundle_options INTEGER,
    output_path TEXT,
    crc INTEGER,
    PRIMARY KEY (id)
);

