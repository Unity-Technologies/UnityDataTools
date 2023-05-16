CREATE TABLE meshes(
    id INTEGER,
    sub_meshes INTEGER,
    blend_shapes INTEGER,
    bones INTEGER,
    indices INTEGER,
    vertices INTEGER,
    compression INTEGER,
    rw_enabled INTEGER,
    vertex_size INTEGER,
    channels TEXT,
    PRIMARY KEY (id)
);

CREATE VIEW mesh_view AS
SELECT
    o.*,
    m.sub_meshes,
    m.blend_shapes,
    m.bones,
    m.indices,
    m.vertices,
    m.compression,
    m.rw_enabled,
    m.vertex_size,
    m.channels
FROM meshes m
INNER JOIN object_view o ON o.id = m.id;
