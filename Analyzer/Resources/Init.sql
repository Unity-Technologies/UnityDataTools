CREATE TABLE IF NOT EXISTS types
(
    id INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

-- Describes a unity archive that contains serialized files and other built content.
-- A common use of the unity archive is for AssetBundles but it can also be used for
-- Player, Content Archive and ContentDirectory builds.
CREATE TABLE IF NOT EXISTS archives
(
    id INTEGER,
    name TEXT,
    file_size INTEGER,
    PRIMARY KEY (id)
);

-- One row per SerializedFile encountered during analysis. The name is often a technical,
-- hash-based string rather than a readable path, because that is how the file is named on disk:
--   * Regular AssetBundles: "CAB-<MD4 hash of the AssetBundle name>". This is MD4, not Unity's
--     Hash128 (spooky hash), and the optional AssetBundle-filename hash is not part of it.
--   * Scene bundles vary by build pipeline: BuildPipeline.BuildAssetBundles uses
--     "BuildPlayer-<SceneName>"; the Scriptable Build Pipeline / Addressables uses
--     "CAB-<hash of scene path>"; the Multi-Process Build Pipeline uses "CAB-<scene GUID>".
--   * Player builds name scenes "level0", "level1", ... in scene-list order.
-- archive references the row from archives table of the unity archive containing this file,
-- or is '' when the file is not inside an unity archive; object_view turns that '' into NULL.
CREATE TABLE IF NOT EXISTS serialized_files
(
    id INTEGER,
    archive INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

-- Records information about each Unity Object discovered in the analyze process.
-- id - unique id for the object (assigned while populating the database, this value does not exist in the serialized content)
-- object_id - Local file id for the object, serialized as m_PathID in the object references (PPTRs). signed 64 bit.
--        This is unique within a serialized file, but not across files.
-- type - references the row in the types table.
-- name - the Object.name property.  In many case this is empty.
-- game_object - only applies to components in a game object hierarchy, otherwise it is not set.
-- crc32 - the CRC of the serialized state of the object, including any external .resS or .resource content.
--        Useful for comparing builds to detect differences
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

-- Tracks all references between Unity Objects (e.g. PPTRs)
-- These references can exist between objects together inside the same serialized file, or they can
-- span between serialized files.
CREATE TABLE IF NOT EXISTS refs
(
    object INTEGER,
    referenced_object INTEGER,
    property_path INTEGER,
    property_type INTEGER
);

-- Resolves the property_path and property_type ids in the refs table to their string values.
CREATE VIEW refs_view AS
SELECT r.object, r.referenced_object, pn.name AS property_path, pt.name AS property_type
FROM refs r
INNER JOIN property_names pn ON r.property_path = pn.id
INNER JOIN property_types pt ON r.property_type = pt.id;

CREATE VIEW object_view AS
SELECT o.id, o.object_id, ab.name AS archive, sf.name AS serialized_file, t.name AS type, o.name, o.game_object, o.size,
CASE
    WHEN size < 1024 THEN printf('%!5.1f B', size * 1.0)
    WHEN size >=  1024 AND size < (1024 * 1024) THEN printf('%!5.1f KB', size / 1024.0)
    WHEN size >= (1024 * 1024)  AND size < (1024 * 1024 * 1024) THEN printf('%!5.1f MB', size / 1024.0 / 1024)
    WHEN size >= (1024 * 1024 * 1024) THEN printf('%!5.1f GB', size / 1024.0 / 1024 / 1024)
END AS pretty_size, o.crc32
FROM objects o
INNER JOIN types t ON o.type = t.id
INNER JOIN serialized_files sf ON o.serialized_file = sf.id
LEFT JOIN archives ab ON sf.archive = ab.id;

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
REPLACE(GROUP_CONCAT(DISTINCT IIF(archive IS NULL, serialized_file, archive)), ',', ',' || CHAR(13)) AS in_files
FROM object_view
GROUP BY name, type, size, crc32
HAVING instances > 1
ORDER BY size DESC, instances DESC;

CREATE VIEW view_material_shader_refs AS
SELECT m.id material_id, m.name material_name, a.name material_path, m.archive material_archive, s.id shader_id, s.name shader_name, s.archive shader_archive
FROM object_view m
INNER JOIN refs_view r ON m.id = r.object AND r.property_path = 'm_Shader'
INNER JOIN object_view s ON r.referenced_object = s.id
LEFT JOIN assetbundle_assets a ON m.id = a.object;

CREATE VIEW view_material_texture_refs AS
SELECT m.id material_id, m.name material_name, a.name material_path, m.archive material_archive, t.id texture_id, t.name texture_name, t.archive texture_archive
FROM object_view m
INNER JOIN refs_view r ON r.object = m.id AND property_type = 'Texture'
INNER JOIN object_view t ON r.referenced_object = t.id
LEFT JOIN assetbundle_assets a ON m.id = a.object
WHERE m.type = 'Material';

-- Special-case type value for the fake Scene object that is sometimes inserted into the object table,
-- see SerializedFileSQLiteWriter for details
INSERT INTO types (id, name) VALUES (-1, 'Scene');

-- Database schema version. Bump when the schema changes in a way that tools relying on it
-- (e.g. find-refs) cannot read from an older database. 1 = normalized refs table (issue #44);
-- 2 = renamed assets/asset_dependencies tables to assetbundle_assets/preload_dependencies
-- (issue #82); 3 = renamed asset_bundles table to archives and the asset_bundle column/alias
-- to archive (issue #68); databases produced before versioning report 0.
PRAGMA user_version = 3;

PRAGMA synchronous = OFF;
PRAGMA journal_mode = MEMORY;
