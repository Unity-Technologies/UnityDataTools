CREATE TABLE shaders(
    id INTEGER,
    decompressed_size INTEGER,
    sub_shaders INTEGER,
    unique_programs INTEGER,
    keywords TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE shader_apis(
    id INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE shader_subprograms(
    shader INTEGER,
    pass INTEGER,
    sub_program INTEGER,
    hw_tier INTEGER,
    shader_type TEXT,
    api INTEGER,
    keywords TEXT
);

CREATE VIEW shader_view AS
SELECT
    o.*,
    s.decompressed_size,
    s.sub_shaders,
    COUNT(*) AS sub_programs,
    s.unique_programs,
    s.keywords
FROM object_view o
INNER JOIN shaders s ON o.id = s.id
LEFT JOIN shader_subprograms sp ON s.id = sp.shader
GROUP BY s.id;

CREATE VIEW view_breakdown_shaders AS
SELECT name, count(*) AS instances,
CASE
    WHEN sum(size) < 1024 THEN printf('%!5.1f B', sum(size) * 1.0)
    WHEN sum(size) >=  1024 AND sum(size) < (1024 * 1024) THEN printf('%!5.1f KB', sum(size) / 1024.0)
    WHEN sum(size) >= (1024 * 1024)  AND sum(size) < (1024 * 1024 * 1024) THEN printf('%!5.1f MB', sum(size) / 1024.0 / 1024)
    WHEN sum(size) >= (1024 * 1024 * 1024) THEN printf('%!5.1f GB', sum(size) / 1024.0 / 1024 / 1024)
END AS pretty_total_size,
sum(size) AS total_size, GROUP_CONCAT(asset_bundle, CHAR(13)) AS in_bundles
FROM shader_view
GROUP BY name
ORDER BY total_size DESC, instances DESC;

CREATE VIEW shader_subprogram_view AS
SELECT s.*, pt.name AS api, sp.pass, sp.hw_tier, sp.shader_type, sp.keywords AS prog_keywords
FROM shader_view s
LEFT JOIN shader_subprograms sp ON s.id = sp.shader
LEFT JOIN shader_apis pt ON pt.id = sp.api;

INSERT INTO shader_apis (name, id)
VALUES
('Unknown', 0),
('GLLegacy_Removed', 1),
('GLES31AEP', 2),
('GLES31', 3),
('GLES3', 4),
('GLES', 5),
('GLCore32', 6),
('GLCore41', 7),
('GLCore43', 8),
('DX9VertexSM20_Removed', 9),
('DX9VertexSM30_Removed', 10),
('DX9PixelSM20_Removed', 11),
('DX9PixelSM30_Removed', 12),
('DX10Level9Vertex_Removed', 13),
('DX10Level9Pixel_Removed', 14),
('DX11VertexSM40', 15),
('DX11VertexSM50', 16),
('DX11PixelSM40', 17),
('DX11PixelSM50', 18),
('DX11GeometrySM40', 19),
('DX11GeometrySM50', 20),
('DX11HullSM50', 21),
('DX11DomainSM50', 22),
('MetalVS', 23),
('MetalFS', 24),
('SPIRV', 25),
('ConsoleVS', 26),
('ConsoleFS', 27),
('ConsoleHS', 28),
('ConsoleDS', 29),
('ConsoleGS', 30);
