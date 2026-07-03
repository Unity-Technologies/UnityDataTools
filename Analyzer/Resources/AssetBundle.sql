-- tables related to the AssetBundle and PreloadData objects

-- Do not confuse the AssetBundle Unity object (the source of much of this data)
-- with the asset_bundles table, which is general to any Unity Archive.

-- The "assets" that an AssetBundle explicitly exposes: each m_Container entry of the AssetBundle
-- object names an object (the addressable/asset name -> object it maps to). Populated only from
-- the AssetBundle object, so this table is empty for Player and ContentDirectory builds.
-- For scene bundles the entry names the scene and points at the synthetic Scene object (see
-- AssetBundleHandler / SerializedFileSQLiteWriter).
CREATE TABLE IF NOT EXISTS assets(
    object INTEGER,
    name TEXT
);

-- object depends on dependency. This table has three sources, only the first of which is truly
-- AssetBundle-specific:
--   * AssetBundleHandler: an asset's slice of the AssetBundle object's m_PreloadTable.
--   * SerializedFileSQLiteWriter: a scene object -> each object in the scene's SerializedFiles.
--   * PreloadDataHandler: the PreloadData object's m_Assets. PreloadData is a *separate* Unity
--     object (not part of the AssetBundle object) and also exists in Player builds, so this table
--     is NOT empty there; but those builds have no scene object, so PreloadDataHandler currently
--     attributes the rows to object id -1 (a known limitation).
CREATE TABLE IF NOT EXISTS asset_dependencies(
    object INTEGER,
    dependency INTEGER
);

CREATE VIEW IF NOT EXISTS asset_view AS
SELECT
    a.name AS asset_name,
    o.*
FROM assets a INNER JOIN object_view o ON o.id = a.object;

CREATE VIEW IF NOT EXISTS asset_dependencies_view AS
SELECT a.id, a.asset_name, a.asset_bundle, a.type, od.id dep_id, od.asset_bundle dep_asset_bundle, od.name dep_name, od.type dep_type
FROM asset_view a
INNER JOIN asset_dependencies d ON a.id = d.object
INNER JOIN object_view od ON od.id = d.dependency;
