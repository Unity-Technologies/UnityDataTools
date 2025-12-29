CREATE TABLE IF NOT EXISTS build_reports(
    id INTEGER,
    build_type TEXT,
    build_result TEXT,
    platform_name TEXT,
    subtarget INTEGER,
    total_time_ticks INTEGER,
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

CREATE VIEW build_report_view AS
SELECT
    o.*,
    br.build_type,
    br.build_result,
    br.platform_name,
    br.subtarget,
    br.total_time_ticks,
    br.total_size,
    br.build_guid,
    br.total_errors,
    br.total_warnings,
    br.options,
    br.asset_bundle_options,
    br.output_path,
    br.crc
FROM object_view o
INNER JOIN build_reports br ON o.id = br.id;
