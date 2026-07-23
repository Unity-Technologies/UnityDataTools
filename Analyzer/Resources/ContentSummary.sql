CREATE TABLE IF NOT EXISTS build_report_content_summary(
    id INTEGER,
    serialized_file_size INTEGER,
    reused_serialized_file_size INTEGER,
    resource_data_size INTEGER,
    header_size INTEGER,
    serialized_file_count INTEGER,
    reused_serialized_file_count INTEGER,
    resource_file_count INTEGER,
    object_count INTEGER,
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS build_report_content_type_stats(
    content_summary_id INTEGER NOT NULL,
    type INTEGER NOT NULL,
    size INTEGER,
    object_count INTEGER,
    resource_count INTEGER,
    PRIMARY KEY (content_summary_id, type),
    FOREIGN KEY (content_summary_id) REFERENCES build_report_content_summary(id)
);

CREATE TABLE IF NOT EXISTS build_report_content_asset_stats(
    content_summary_id INTEGER NOT NULL,
    source_asset_guid TEXT,
    source_asset_path TEXT,
    size INTEGER,
    object_count INTEGER,
    resource_count INTEGER,
    FOREIGN KEY (content_summary_id) REFERENCES build_report_content_summary(id)
);

-- Cross-build statistics with the owning BuildReport resolved. build_report_id is the id of the
-- BuildReport object (type 1125) in the same serialized file as the ContentSummary; it is not
-- stored on the table, so this view computes it the same way build_report_packed_assets_view does.
CREATE VIEW build_report_content_summary_view AS
SELECT
    cs.id AS content_summary_id,
    br_obj.id AS build_report_id,
    sf.name AS build_report_filename,
    cs.serialized_file_size,
    cs.reused_serialized_file_size,
    cs.resource_data_size,
    cs.header_size,
    cs.serialized_file_count,
    cs.reused_serialized_file_count,
    cs.resource_file_count,
    cs.object_count
FROM build_report_content_summary cs
INNER JOIN objects o ON cs.id = o.id
INNER JOIN serialized_files sf ON o.serialized_file = sf.id
LEFT JOIN objects br_obj ON o.serialized_file = br_obj.serialized_file AND br_obj.type = 1125;

-- Per-type statistics with the type name resolved (from TypeIdRegistry or TypeTree analysis) and
-- the owning BuildReport, so a single build's type breakdown can be selected by build_report_id.
CREATE VIEW build_report_content_type_stats_view AS
SELECT
    cs.id AS content_summary_id,
    br_obj.id AS build_report_id,
    sf.name AS build_report_filename,
    cts.type,
    COALESCE(t.name, CAST(cts.type AS TEXT)) AS type_name,
    cts.size,
    cts.object_count,
    cts.resource_count
FROM build_report_content_type_stats cts
INNER JOIN build_report_content_summary cs ON cts.content_summary_id = cs.id
INNER JOIN objects o ON cs.id = o.id
INNER JOIN serialized_files sf ON o.serialized_file = sf.id
LEFT JOIN objects br_obj ON o.serialized_file = br_obj.serialized_file AND br_obj.type = 1125
LEFT JOIN types t ON cts.type = t.id;
