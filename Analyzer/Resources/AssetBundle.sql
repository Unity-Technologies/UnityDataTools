CREATE TABLE assets(
    object INTEGER,
    name TEXT
);

CREATE TABLE asset_dependencies(
    object INTEGER,
    dependency INTEGER
);

CREATE VIEW asset_view AS
SELECT
    a.name AS asset_name,
    o.*
FROM assets a INNER JOIN object_view o ON o.id = a.object;

CREATE VIEW asset_dependencies_view AS
SELECT a.id, a.asset_name, a.asset_bundle, a.type, od.id dep_id, od.asset_bundle dep_asset_bundle, od.name dep_name, od.type dep_type
FROM asset_view a
INNER JOIN asset_dependencies d ON a.id = d.object
INNER JOIN object_view od ON od.id = d.dependency;