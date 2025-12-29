CREATE TABLE IF NOT EXISTS packed_assets(
    id INTEGER,
    path TEXT,
    file_header_size INTEGER,
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS packed_asset_contents(
    packed_assets_id INTEGER,
    object_id INTEGER,
    type INTEGER,
    size INTEGER,
    offset INTEGER,
    source_asset_guid TEXT,
    build_time_asset_path TEXT,
    FOREIGN KEY (packed_assets_id) REFERENCES packed_assets(id)
);

CREATE VIEW packed_assets_view AS
SELECT
    o.*,
    pa.path,
    pa.file_header_size
FROM object_view o
INNER JOIN packed_assets pa ON o.id = pa.id;

CREATE VIEW packed_asset_contents_view AS
SELECT
    pac.packed_assets_id,
    pac.object_id,
    pac.type,
    t.name as type_name,
    pac.size,
    pac.offset,
    pac.source_asset_guid,
    pac.build_time_asset_path,
    pa.path
FROM packed_asset_contents pac
LEFT JOIN packed_assets pa ON pac.packed_assets_id = pa.id
LEFT JOIN types t ON pac.type = t.id;

