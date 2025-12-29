CREATE TABLE IF NOT EXISTS build_reports(
    id INTEGER,
    build_guid TEXT,
    platform_name TEXT,
    subtarget INTEGER,
    options INTEGER,
    asset_bundle_options INTEGER,
    output_path TEXT,
    crc INTEGER,
    total_size INTEGER,
    total_time_ticks INTEGER,
    total_errors INTEGER,
    total_warnings INTEGER,
    build_type TEXT,
    build_result TEXT,
    PRIMARY KEY (id)
);

CREATE VIEW build_report_view AS
SELECT
    o.*,
    br.build_guid,
    br.platform_name,
    br.subtarget,
    br.options,
    br.asset_bundle_options,
    br.output_path,
    br.crc,
    br.total_size,
    br.total_time_ticks,
    br.total_errors,
    br.total_warnings,
    br.build_type,
    br.build_result
FROM object_view o
INNER JOIN build_reports br ON o.id = br.id;
