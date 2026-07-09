CREATE TABLE IF NOT EXISTS monoscripts(
    id INTEGER,
    class_name TEXT,
    namespace TEXT,
    assembly_name TEXT,
    PRIMARY KEY (id)
);

CREATE VIEW monoscript_view AS
SELECT
    o.id,
    o.object_id,
    o.archive,
    o.serialized_file,
    m.class_name,
    m.namespace,
    m.assembly_name
FROM object_view o INNER JOIN monoscripts m ON o.id = m.id;

CREATE VIEW script_object_view AS
SELECT
    mb.id,
    mb.object_id,
    mb.archive,
    mb.serialized_file,
    ms.class_name,
    ms.namespace,
    ms.assembly_name,
    mb.name,
    mb.size
FROM object_view mb
INNER JOIN refs_view r ON mb.id = r.object
INNER JOIN monoscript_view ms ON r.referenced_object = ms.id
WHERE mb.type = 'MonoBehaviour' AND r.property_type = 'MonoScript';
