CREATE TABLE assets(
    object INTEGER,
    name TEXT
);

CREATE VIEW asset_view AS
SELECT
    a.name AS asset_name,
    o.*
FROM assets a INNER JOIN object_view o ON o.id = a.object;
