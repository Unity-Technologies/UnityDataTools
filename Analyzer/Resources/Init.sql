CREATE TABLE types
(
    id INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE asset_bundles
(
    id INTEGER,
    name TEXT,
    file_size INTEGER,
    PRIMARY KEY (id)
);

CREATE TABLE serialized_files
(
    id INTEGER,
    asset_bundle INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE objects
(
    id INTEGER,
    object_id INTEGER,
    serialized_file INTEGER,
    type INTEGER,
    name TEXT,
    game_object INTEGER,
    size INTEGER,
    PRIMARY KEY (id)
);

CREATE TABLE refs
(
    object INTEGER,
    referenced_object INTEGER,
    property_path TEXT
);

CREATE VIEW object_view AS
SELECT o.id, o.object_id, ab.name AS asset_bundle, sf.name AS serialized_file, t.name AS type, o.name, o.game_object, o.size,
CASE
    WHEN size < 1024 THEN printf('%!5.1f B', size * 1.0)
    WHEN size >=  1024 AND size < (1024 * 1024) THEN printf('%!5.1f KB', size / 1024.0)
    WHEN size >= (1024 * 1024)  AND size < (1024 * 1024 * 1024) THEN printf('%!5.1f MB', size / 1024.0 / 1024)
    WHEN size >= (1024 * 1024 * 1024) THEN printf('%!5.1f GB', size / 1024.0 / 1024 / 1024)
END AS pretty_size
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
REPLACE(GROUP_CONCAT(DISTINCT asset_bundle), ',', ',' || CHAR(13)) AS in_bundles
FROM object_view
GROUP BY name, type, size
HAVING instances > 1
ORDER BY size DESC, instances DESC;

PRAGMA synchronous = OFF;
PRAGMA journal_mode = MEMORY;
