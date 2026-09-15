CREATE TABLE IF NOT EXISTS assetbundle_assets(
    -- The assets an AssetBundle explicitly exposes: one row per m_Container entry of the
    -- AssetBundle object. Empty for Player and ContentDirectory builds, which have no such object.
    object INTEGER,     -- objects.id; for a scene bundle this is the synthetic Scene object
    name TEXT           -- the container path the asset is addressed by
);

CREATE TABLE IF NOT EXISTS preload_dependencies(
    -- Objects that Unity preloads alongside another object. Populated for AssetBundle and Player
    -- builds, but not ContentDirectory builds. See Documentation/analyzer-schema.md.
    object INTEGER,     -- objects.id: an AssetBundle asset, a synthetic Scene, or a PreloadData object
    dependency INTEGER  -- objects.id, or dangling_refs.id when the target was not analyzed
);

CREATE VIEW IF NOT EXISTS assetbundle_asset_view AS
-- AssetBundle assets with their object columns resolved. Inner join, so an asset whose object was
-- not analyzed is omitted here but still present in assetbundle_assets.
SELECT
    a.name AS asset_name,
    o.*
FROM assetbundle_assets a INNER JOIN object_view o ON o.id = a.object;

CREATE VIEW IF NOT EXISTS preload_dependencies_view AS
-- Preload dependencies of AssetBundle assets and scenes, with both sides resolved. Narrower than
-- the table: Player-build rows and dangling dependencies drop out of the inner joins.
SELECT a.id, a.asset_name, a.archive, a.type, od.id dep_id, od.archive dep_archive, od.name dep_name, od.type dep_type
FROM assetbundle_asset_view a
INNER JOIN preload_dependencies d ON a.id = d.object
INNER JOIN object_view od ON od.id = d.dependency;
