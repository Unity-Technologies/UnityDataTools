CREATE TABLE IF NOT EXISTS types
(
    id INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS asset_bundles
(
    id INTEGER,
    name TEXT,
    file_size INTEGER,
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS serialized_files
(
    id INTEGER,
    asset_bundle INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS objects
(
    id INTEGER,
    object_id INTEGER,
    serialized_file INTEGER,
    type INTEGER,
    name TEXT,
    game_object INTEGER,
    size INTEGER,
    crc32 INTEGER,
    PRIMARY KEY (id)
);

-- Deduplicated lookup tables for the strings referenced by the refs table.
-- refs stores ids into these instead of repeating the strings on every row.
CREATE TABLE IF NOT EXISTS property_names
(
    id INTEGER PRIMARY KEY,
    name TEXT
);

CREATE TABLE IF NOT EXISTS property_types
(
    id INTEGER PRIMARY KEY,
    name TEXT
);

CREATE TABLE IF NOT EXISTS refs
(
    object INTEGER,
    referenced_object INTEGER,
    property_path INTEGER,
    property_type INTEGER
);

-- Reproduces the pre-normalization refs shape (property_path/property_type as text)
-- so queries can read the strings without joining the lookup tables by hand.
-- INNER JOIN: every refs row is written with both ids present and their lookup rows
-- inserted in the same transaction, so the joins always match (the ids are foreign keys).
CREATE VIEW refs_view AS
SELECT r.object, r.referenced_object, pn.name AS property_path, pt.name AS property_type
FROM refs r
INNER JOIN property_names pn ON r.property_path = pn.id
INNER JOIN property_types pt ON r.property_type = pt.id;

CREATE VIEW object_view AS
SELECT o.id, o.object_id, ab.name AS asset_bundle, sf.name AS serialized_file, t.name AS type, o.name, o.game_object, o.size,
CASE
    WHEN size < 1024 THEN printf('%!5.1f B', size * 1.0)
    WHEN size >=  1024 AND size < (1024 * 1024) THEN printf('%!5.1f KB', size / 1024.0)
    WHEN size >= (1024 * 1024)  AND size < (1024 * 1024 * 1024) THEN printf('%!5.1f MB', size / 1024.0 / 1024)
    WHEN size >= (1024 * 1024 * 1024) THEN printf('%!5.1f GB', size / 1024.0 / 1024 / 1024)
END AS pretty_size, o.crc32
FROM objects o
INNER JOIN types t ON o.type = t.id
INNER JOIN serialized_files sf ON o.serialized_file = sf.id
LEFT JOIN asset_bundles ab ON sf.asset_bundle = ab.id;

CREATE VIEW view_breakdown_by_type AS
SELECT *,
CASE
	WHEN byte_size < 1024 THEN printf('%!5.1f B', byte_size * 1.0)
	WHEN byte_size >=  1024 AND byte_size < (1024 * 1024) THEN printf('%!5.1f KB', byte_size / 1024.0)
	WHEN byte_size >= (1024 * 1024)  AND byte_size < (1024 * 1024 * 1024) THEN printf('%!5.1f MB', byte_size / 1024.0 / 1024)
	WHEN byte_size >= (1024 * 1024 * 1024) THEN printf('%!5.1f GB', byte_size / 1024.0 / 1024 / 1024)
END AS pretty_size
FROM
(SELECT type, count(*) AS count, sum(size) AS byte_size
FROM object_view AS o
GROUP BY type
ORDER BY byte_size DESC, count DESC);

CREATE VIEW view_potential_duplicates AS
SELECT COUNT(name) AS instances, name, type,
CASE
	WHEN sum(size) < 1024 THEN printf('%!5.1f B', sum(size) * 1.0)
	WHEN sum(size) >=  1024 AND sum(size) < (1024 * 1024) THEN printf('%!5.1f KB', sum(size) / 1024.0)
	WHEN sum(size) >= (1024 * 1024)  AND sum(size) < (1024 * 1024 * 1024) THEN printf('%!5.1f MB', sum(size) / 1024.0 / 1024)
	WHEN sum(size) >= (1024 * 1024 * 1024) THEN printf('%!5.1f GB', sum(size) / 1024.0 / 1024 / 1024)
END AS pretty_total_size,
sum(size) AS total_size,
size,
pretty_size,
REPLACE(GROUP_CONCAT(DISTINCT IIF(asset_bundle IS NULL, serialized_file, asset_bundle)), ',', ',' || CHAR(13)) AS in_files
FROM object_view
GROUP BY name, type, size, crc32
HAVING instances > 1
ORDER BY size DESC, instances DESC;

CREATE VIEW view_material_shader_refs AS
SELECT m.id material_id, m.name material_name, a.name material_path, m.asset_bundle material_asset_bundle, s.id shader_id, s.name shader_name, s.asset_bundle shader_asset_bundle
FROM object_view m
INNER JOIN refs_view r ON m.id = r.object AND r.property_path = 'm_Shader'
INNER JOIN object_view s ON r.referenced_object = s.id
LEFT JOIN assets a ON m.id = a.object;

CREATE VIEW view_material_texture_refs AS
SELECT m.id material_id, m.name material_name, a.name material_path, m.asset_bundle material_asset_bundle, t.id texture_id, t.name texture_name, t.asset_bundle texture_asset_bundle
FROM object_view m
INNER JOIN refs_view r ON r.object = m.id AND property_type = 'Texture'
INNER JOIN object_view t ON r.referenced_object = t.id
LEFT JOIN assets a ON m.id = a.object
WHERE m.type = 'Material';

INSERT INTO types (id, name) VALUES (-1, 'Scene');

-- Database schema version. Bump when the schema changes in a way that tools relying on it
-- (e.g. find-refs) cannot read from an older database. 1 = normalized refs table (issue #44);
-- databases produced before versioning report 0.
PRAGMA user_version = 1;

PRAGMA synchronous = OFF;
PRAGMA journal_mode = MEMORY;
