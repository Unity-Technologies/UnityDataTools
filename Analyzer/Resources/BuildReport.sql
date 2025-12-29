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

CREATE TABLE IF NOT EXISTS build_report_files(
    build_report_id INTEGER NOT NULL,
    file_index INTEGER NOT NULL,
    path TEXT NOT NULL,
    role TEXT NOT NULL,
    size INTEGER NOT NULL,
    PRIMARY KEY (build_report_id, file_index),
    FOREIGN KEY (build_report_id) REFERENCES build_reports(id)
);

CREATE VIEW build_report_files_view AS
SELECT
    br.id AS build_report_id,
    br.build_type,
    br.platform_name,
    brf.file_index,
    brf.path,
    brf.role,
    brf.size
FROM build_report_files brf
INNER JOIN build_reports br ON brf.build_report_id = br.id;

