CREATE TABLE IF NOT EXISTS types
(
    -- Unity type names, referenced by objects.type.
    id INTEGER,   -- Unity class id; -1 is the synthetic Scene type analyze adds for scenes
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS archives
(
    -- One row per Unity Archive: AssetBundles, and the archives inside Player and
    -- ContentDirectory builds. Full reference: Documentation/analyzer-schema.md.
    id INTEGER,
    name TEXT,          -- UNIQUE: analyze covers a single build, so names must not collide
    file_size INTEGER,
    PRIMARY KEY (id),
    UNIQUE (name)
);

CREATE TABLE IF NOT EXISTS serialized_files
(
    -- One row per SerializedFile analyzed.
    id INTEGER,
    archive INTEGER,    -- archives.id, or '' when the file is not inside an archive
    name TEXT,          -- on-disk name, usually hash-based: CAB-<hash>, BuildPlayer-<scene>, levelN
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS objects
(
    -- One row per Unity object found. Query object_view for the resolved type, file and
    -- archive names.
    id INTEGER,              -- analyzer-assigned; does not exist in the serialized data
    object_id INTEGER,       -- Unity local file id (m_PathID), signed 64-bit, unique only within its file
    serialized_file INTEGER, -- serialized_files.id
    type INTEGER,            -- types.id
    name TEXT,               -- Object.name; often empty
    game_object INTEGER,     -- objects.id of the owning GameObject; '' when not a component
    size INTEGER,            -- includes external .resS / .resource bytes
    crc32 INTEGER,           -- CRC of the serialized state incl. stream data; 0 when --skip-crc was used
    PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS property_names
(
    -- Distinct property paths referenced by refs.property_path. Join through refs_view.
    id INTEGER PRIMARY KEY,
    name TEXT               -- e.g. m_Shader, m_Materials[0]
);

CREATE TABLE IF NOT EXISTS property_types
(
    -- Distinct referenced type names referenced by refs.property_type. Join through refs_view.
    id INTEGER PRIMARY KEY,
    name TEXT               -- e.g. Texture2D, MonoScript
);

CREATE TABLE IF NOT EXISTS refs
(
    -- References between Unity objects (PPtrs), within and across serialized files.
    -- Empty when analyze was run with --skip-references.
    object INTEGER,             -- objects.id
    referenced_object INTEGER,  -- objects.id, or dangling_refs.id when the target was not analyzed
    property_path INTEGER,      -- property_names.id
    property_type INTEGER       -- property_types.id
);

CREATE TABLE IF NOT EXISTS dangling_refs
(
    -- Reference targets that were never analyzed because their serialized file was not part of the
    -- input. Every id used by refs resolves to exactly one of objects or dangling_refs.
    id INTEGER,                 -- the assigned object id; no objects row has it
    object_id INTEGER,          -- the target's local file id (m_PathID) within its file
    serialized_file INTEGER,    -- serialized_files.id of the un-analyzed file; it has no objects
    PRIMARY KEY (id)
);

CREATE VIEW refs_view AS
-- refs with the property_path and property_type ids resolved to their strings.
SELECT r.object, r.referenced_object, pn.name AS property_path, pt.name AS property_type
FROM refs r
INNER JOIN property_names pn ON r.property_path = pn.id
INNER JOIN property_types pt ON r.property_type = pt.id;

CREATE VIEW dangling_refs_view AS
-- One row per reference to an un-analyzed target. Empty with --skip-references.
SELECT
    r.object AS source_id,
    src_sf.name AS source_serialized_file,
    src_o.object_id AS source_object_id,
    pn.name AS property_path,
    pt.name AS property_type,
    d.id AS target_id,
    tgt_sf.name AS target_serialized_file,
    d.object_id AS target_object_id
FROM dangling_refs d
INNER JOIN refs r ON r.referenced_object = d.id
INNER JOIN objects src_o ON r.object = src_o.id
INNER JOIN serialized_files src_sf ON src_o.serialized_file = src_sf.id
INNER JOIN serialized_files tgt_sf ON d.serialized_file = tgt_sf.id
LEFT JOIN property_names pn ON r.property_path = pn.id
LEFT JOIN property_types pt ON r.property_type = pt.id;

CREATE VIEW object_view AS
-- The main view: every analyzed object with its type, file and archive names resolved. Start here.
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
-- Object count and total size per type, largest first.
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
-- Objects with identical name, type, size and crc32 found in more than one file. --skip-crc sets
-- every crc32 to 0, which makes this view produce many false positives.
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
-- Each Material and the Shader it references, with the AssetBundle path when there is one.
SELECT m.id material_id, m.name material_name, a.name material_path, m.archive material_archive, s.id shader_id, s.name shader_name, s.archive shader_archive
FROM object_view m
INNER JOIN refs_view r ON m.id = r.object AND r.property_path = 'm_Shader'
INNER JOIN object_view s ON r.referenced_object = s.id
LEFT JOIN assetbundle_assets a ON m.id = a.object;

CREATE VIEW view_material_texture_refs AS
-- Each Material and the Textures it references, with the AssetBundle path when there is one.
SELECT m.id material_id, m.name material_name, a.name material_path, m.archive material_archive, t.id texture_id, t.name texture_name, t.archive texture_archive
FROM object_view m
INNER JOIN refs_view r ON r.object = m.id AND property_type = 'Texture'
INNER JOIN object_view t ON r.referenced_object = t.id
LEFT JOIN assetbundle_assets a ON m.id = a.object
WHERE m.type = 'Material';

INSERT INTO types (id, name) VALUES (-1, 'Scene');

PRAGMA user_version = 8;

PRAGMA synchronous = OFF;
PRAGMA journal_mode = MEMORY;
