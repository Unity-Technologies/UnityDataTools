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
    crc32 INTEGER,
    PRIMARY KEY (id)
);

CREATE TABLE refs
(
    object INTEGER,
    referenced_object INTEGER,
    property_path TEXT,
    property_type TEXT
);

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
INNER JOIN refs r ON m.id = r.object AND r.property_path = 'm_Shader'
INNER JOIN object_view s ON r.referenced_object = s.id
LEFT JOIN assets a ON m.id = a.object;

CREATE VIEW view_material_texture_refs AS
SELECT m.id material_id, m.name material_name, a.name material_path, m.asset_bundle material_asset_bundle, t.id texture_id, t.name texture_name, t.asset_bundle texture_asset_bundle
FROM object_view m
INNER JOIN refs r ON r.object = m.id AND property_type = "Texture"
INNER JOIN object_view t ON r.referenced_object = t.id
LEFT JOIN assets a ON m.id = a.object
WHERE m.type = "Material";

INSERT INTO types (id, name) VALUES (-1, 'Scene');

CREATE TABLE addr_builds
(
    id INTEGER,
    name TEXT,
    build_target INTEGER,
    start_time TEXT,
    duration REAL,
    error TEXT,
    package_version TEXT,
    player_version TEXT,
    build_script TEXT,
    result_hash TEXT,
    type INTEGER,
    unity_version TEXT,
    PRIMARY KEY (id)
);

create table addr_build_bundles
(
    id INTEGER,
    build_id INTEGER,
    asset_count INTEGER,
    build_status INTEGER,
    crc INTEGER,
    compression TEXT,
    dependency_file_size INTEGER,
    expanded_dependency_file_size INTEGER,
    file_size INTEGER,
    group_rid INTEGER,
    hash TEXT,
    internal_name TEXT,
    load_path TEXT,
    name TEXT,
    provider TEXT,
    result_type TEXT,
    PRIMARY KEY (id, build_id)
);
create table addr_build_bundle_dependent_bundles
(
    bundle_id INTEGER,
    build_id INTEGER,
    dependent_bundle_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, dependent_bundle_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
);
create table addr_build_bundle_dependencies
(
    bundle_id INTEGER,
    build_id INTEGER,
    dependency_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, dependency_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
);
create table addr_build_bundle_expanded_dependencies
(
    bundle_id INTEGER,
    build_id INTEGER,
    dependency_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, dependency_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
);

create table addr_build_bundle_files
(
    bundle_id INTEGER,
    build_id INTEGER,
    file_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, file_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
);
    create table addr_build_bundle_regular_dependencies
(
    bundle_id INTEGER,
    build_id INTEGER,
    dependency_rid INTEGER,
    PRIMARY KEY (bundle_id, build_id, dependency_rid),
    FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
);
    create table addr_build_data_from_other_assets
(
    id INTEGER,
    build_id INTEGER,
    asset_guid TEXT,
    asset_path TEXT,
    file INTEGER,
    main_asset_type INTEGER,
    object_count INTEGER,
    serialized_size INTEGER,
    streamed_size INTEGER,
    PRIMARY KEY (id, build_id)
);
create table addr_build_data_from_other_asset_objects
(
    data_from_other_asset_id INTEGER,
    build_id INTEGER,
    asset_type INTEGER,
    component_name TEXT,
    local_identifier_in_file INTEGER,
    object_name TEXT,
    serialized_size INTEGER,
    streamed_size INTEGER,
    PRIMARY KEY (data_from_other_asset_id, build_id, local_identifier_in_file),
    FOREIGN KEY (data_from_other_asset_id, build_id) REFERENCES addr_build_data_from_other_assets(id, build_id)
);
create table addr_build_data_from_other_asset_object_references
(
    data_from_other_asset_id INTEGER,
    build_id INTEGER,
    local_identifier_in_file INTEGER,
    asset_id INTEGER,
    object_id INTEGER,
    PRIMARY KEY (data_from_other_asset_id, build_id, local_identifier_in_file, asset_id, object_id),
    FOREIGN KEY (data_from_other_asset_id, build_id, local_identifier_in_file) REFERENCES addr_build_data_from_other_asset_objects(data_from_other_asset_id, build_id, local_identifier_in_file)
);
create table addr_build_data_from_other_asset_referencing_assets
(
    data_from_other_asset_id INTEGER,
    build_id INTEGER,
    referencing_asset_rid INTEGER,
    PRIMARY KEY (data_from_other_asset_id, build_id, referencing_asset_rid),
    FOREIGN KEY (data_from_other_asset_id, build_id) REFERENCES addr_build_data_from_other_assets(id, build_id)
);
create table addr_build_explicit_assets
(
    id INTEGER,
    build_id INTEGER,
    bundle INTEGER,
    file INTEGER,
    asset_hash TEXT,
    asset_path TEXT,
    addressable_name TEXT,
    group_guid TEXT,
    guid TEXT,
    internal_id TEXT,
    main_asset_type INTEGER,
    serialized_size INTEGER,
    streamed_size INTEGER,
    PRIMARY KEY (id, build_id)
);
create table addr_build_explicit_asset_externally_referenced_assets
(
    explicit_asset_id INTEGER,
    build_id INTEGER,
    externally_referenced_asset_rid INTEGER,
    PRIMARY KEY (explicit_asset_id, build_id, externally_referenced_asset_rid),
    FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addr_build_explicit_assets(id, build_id)
);
create table addr_build_explicit_asset_internal_referenced_explicit_assets
(
    explicit_asset_id INTEGER,
    build_id INTEGER,
    internal_referenced_explicit_asset_rid INTEGER,
    PRIMARY KEY (explicit_asset_id, build_id, internal_referenced_explicit_asset_rid),
    FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addr_build_explicit_assets(id, build_id)
);
create table addr_build_explicit_asset_internal_referenced_other_assets
(
    explicit_asset_id INTEGER,
    build_id INTEGER,
    internal_referenced_other_asset_rid INTEGER,
    PRIMARY KEY (explicit_asset_id, build_id, internal_referenced_other_asset_rid),
    FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addr_build_explicit_assets(id, build_id)
);
create table addr_build_explicit_asset_labels
(
    explicit_asset_id INTEGER,
    build_id INTEGER,
    label TEXT,
    PRIMARY KEY (explicit_asset_id, build_id, label),
    FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addr_build_explicit_assets(id, build_id)
);
create table addr_build_files
(
    id INTEGER,
    build_id INTEGER,
    bundle INTEGER,
    bundle_object_info_size INTEGER,
    mono_script_count INTEGER,
    mono_script_size INTEGER,
    name TEXT,
    preload_info_size INTEGER,
    write_result_filename TEXT,
    PRIMARY KEY (id, build_id)
);
create table addr_build_file_assets
(
    file_id INTEGER,
    build_id INTEGER,
    asset_rid INTEGER,
    PRIMARY KEY (file_id, build_id, asset_rid),
    FOREIGN KEY (file_id, build_id) REFERENCES addr_build_files(id, build_id)
);
create table addr_build_file_other_assets
(
    file_id INTEGER,
    build_id INTEGER,
    other_asset_rid INTEGER,
    PRIMARY KEY (file_id, build_id, other_asset_rid),
    FOREIGN KEY (file_id, build_id) REFERENCES addr_build_files(id, build_id)
);
create table addr_build_file_sub_files
(
    file_id INTEGER,
    build_id INTEGER,
    sub_file_rid INTEGER,
    PRIMARY KEY (file_id, build_id, sub_file_rid),
    FOREIGN KEY (file_id, build_id) REFERENCES addr_build_files(id, build_id)
);
create table addr_build_file_external_references
(
    file_id INTEGER,
    build_id INTEGER,
    external_reference_rid INTEGER,
    PRIMARY KEY (file_id, build_id, external_reference_rid),
    FOREIGN KEY (file_id, build_id) REFERENCES addr_build_files(id, build_id)
);
create table addr_build_groups
(
    id INTEGER,
    build_id INTEGER,
    guid TEXT,
    name TEXT,
    packing_mode TEXT,
    PRIMARY KEY (id, build_id)
);
create table addr_build_group_bundles
(
    group_id INTEGER,
    build_id INTEGER,
    bundle_rid INTEGER,
    PRIMARY KEY (group_id, build_id, bundle_rid),
    FOREIGN KEY (group_id, build_id) REFERENCES addr_build_groups(id, build_id)
);
create table addr_build_group_schemas
(
    group_id INTEGER,
    build_id INTEGER,
    schema_rid INTEGER,
    PRIMARY KEY (group_id, build_id, schema_rid),
    FOREIGN KEY (group_id, build_id) REFERENCES addr_build_groups(id, build_id)
);
create table addr_build_schemas
(
    id INTEGER,
    build_id INTEGER,
    guid TEXT,
    type TEXT,
    PRIMARY KEY (id, build_id)
);
create table addr_build_schema_data_pairs
(
    schema_id INTEGER,
    build_id INTEGER,
    key TEXT,
    value TEXT,
    PRIMARY KEY (schema_id, build_id, key),
    FOREIGN KEY (schema_id, build_id) REFERENCES addr_build_schemas(id, build_id)
);
create table addr_build_sub_files
(
    id INTEGER,
    build_id INTEGER,
    is_serialized_file INTEGER,
    name TEXT,
    size INTEGER,
    PRIMARY KEY (id, build_id)
);
PRAGMA synchronous = OFF;
PRAGMA journal_mode = MEMORY;
