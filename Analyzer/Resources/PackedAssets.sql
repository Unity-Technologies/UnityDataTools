CREATE TABLE IF NOT EXISTS build_report_packed_assets(
    id INTEGER,
    path TEXT,
    file_header_size INTEGER,
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS build_report_source_assets(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source_asset_guid TEXT NOT NULL,
    build_time_asset_path TEXT NOT NULL,
    UNIQUE(source_asset_guid, build_time_asset_path)
);

CREATE TABLE IF NOT EXISTS build_report_packed_asset_info(
    packed_assets_id INTEGER,
    object_id INTEGER,
    type INTEGER,
    size INTEGER,
    offset INTEGER,
    source_asset_id INTEGER NOT NULL,
    FOREIGN KEY (packed_assets_id) REFERENCES build_report_packed_assets(id),
    FOREIGN KEY (source_asset_id) REFERENCES build_report_source_assets(id)
);

CREATE VIEW build_report_packed_assets_view AS
SELECT
    o.id,
    o.object_id,
    o.serialized_file,
    pa.path,
    pa.file_header_size
FROM object_view o
INNER JOIN build_report_packed_assets pa ON o.id = pa.id;

CREATE VIEW build_report_packed_asset_contents_view AS
SELECT
    o.serialized_file,
    pa.path,
    pac.packed_assets_id,
    pac.object_id,
    pac.type,
    pac.size,
    pac.offset,
    sa.source_asset_guid,
    sa.build_time_asset_path
FROM build_report_packed_asset_info pac
LEFT JOIN build_report_packed_assets pa ON pac.packed_assets_id = pa.id
LEFT JOIN object_view o ON o.id = pa.id
LEFT JOIN build_report_source_assets sa ON pac.source_asset_id = sa.id;

