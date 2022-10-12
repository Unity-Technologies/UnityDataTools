CREATE TABLE shaders(
    id INTEGER,
    decompressed_size INTEGER,
    unique_programs INTEGER,
    PRIMARY KEY (id)
);

CREATE TABLE shader_keywords(
    id INTEGER,
    keyword TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE shader_subprogram_keywords(
    subprogram_id INTEGER,
    keyword_id INTEGER
);

CREATE TABLE shader_apis(
    id INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE shader_subprograms(
    id INTEGER,
    shader INTEGER,
    sub_shader INTEGER,
    pass INTEGER,
    pass_name TEXT,
    sub_program INTEGER,
    hw_tier INTEGER,
    shader_type TEXT,
    api INTEGER,
    PRIMARY KEY(id)
);

CREATE VIEW shader_view AS
SELECT
    o.*,
    s.decompressed_size,
	(SELECT MAX(sub_shader) FROM shader_subprograms sp WHERE s.id = sp.shader) + 1 AS sub_shaders,
    (SELECT COUNT(*) FROM shader_subprograms sp WHERE s.id = sp.shader) AS sub_programs,
    s.unique_programs,
    (
		SELECT GROUP_CONCAT(k.keyword, ',' || CHAR(13)) FROM
		(
			SELECT DISTINCT kp.keyword_id FROM
			shader_subprograms sp
			INNER JOIN shader_subprogram_keywords kp ON sp.id = kp.subprogram_id
			WHERE sp.shader = s.id
		)
		INNER JOIN shader_keywords k ON keyword_id = k.id
	) AS keywords
FROM object_view o
INNER JOIN shaders s ON o.id = s.id;

CREATE VIEW view_breakdown_shaders AS
SELECT name, count(*) AS instances,
CASE
    WHEN sum(size) < 1024 THEN printf('%!5.1f B', sum(size) * 1.0)
    WHEN sum(size) >=  1024 AND sum(size) < (1024 * 1024) THEN printf('%!5.1f KB', sum(size) / 1024.0)
    WHEN sum(size) >= (1024 * 1024)  AND sum(size) < (1024 * 1024 * 1024) THEN printf('%!5.1f MB', sum(size) / 1024.0 / 1024)
    WHEN sum(size) >= (1024 * 1024 * 1024) THEN printf('%!5.1f GB', sum(size) / 1024.0 / 1024 / 1024)
END AS pretty_total_size,
sum(size) AS total_size, GROUP_CONCAT(asset_bundle, ',' || CHAR(13)) AS in_bundles
FROM shader_view
GROUP BY name
ORDER BY total_size DESC, instances DESC;

CREATE VIEW shader_subprogram_view AS
SELECT sp.shader AS shader_id, o.name, sp.sub_shader, sp.hw_tier, api.name api, sp.pass, sp.pass_name, sp.shader_type, sp.sub_program, GROUP_CONCAT(k.keyword, ',' || CHAR(13)) AS keywords
FROM shader_subprograms sp
CROSS JOIN objects o ON o.id = sp.shader
CROSS JOIN shader_apis api ON api.id = sp.api
CROSS JOIN shader_subprogram_keywords sk ON sk.subprogram_id = sp.id
CROSS JOIN shader_keywords k ON sk.keyword_id = k.id
GROUP BY sp.id;

CREATE VIEW shader_keyword_ratios AS
SELECT t.shader_id, o.name, t.sub_shader, t.hw_tier, t.pass, api.name AS api, t.pass_name, t.shader_type, t.total_variants, k.keyword, t.variants, t.ratio
FROM
(
	SELECT sp.shader AS shader_id, sp.sub_shader, sp.hw_tier, sp.api, sp.pass, sp.pass_name, sp.shader_type, sp.total_variants, sk.keyword_id,
	COUNT(*) AS variants,
	printf('%.3f', CAST(COUNT(*) AS FLOAT) / sp.total_variants) AS ratio
	FROM
	(
		SELECT id, shader, sub_shader, hw_tier, api, pass, pass_name, shader_type,
		COUNT(id) OVER(PARTITION BY shader, sub_shader, hw_tier, api, pass, shader_type) AS total_variants
		FROM shader_subprograms
	) sp
	INNER JOIN shader_subprogram_keywords sk ON sk.subprogram_id = sp.id
	GROUP BY shader_id, sp.sub_shader, sp.hw_tier, sp.api, sp.pass, sp.shader_type, sk.keyword_id
	ORDER BY shader_id, sp.sub_shader, sp.hw_tier, sp.api, sp.pass, sp.shader_type, ratio DESC
) t
CROSS JOIN objects o ON o.id = t.shader_id
CROSS JOIN shader_apis api ON api.id = t.api
CROSS JOIN shader_keywords k ON k.id = t.keyword_id;

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
