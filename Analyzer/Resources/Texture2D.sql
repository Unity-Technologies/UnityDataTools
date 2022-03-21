CREATE TABLE texture_formats
(
    id INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE textures
(
    id INTEGER,
    width INTEGER,
    height INTEGER,
    format INTEGER,
    mip_count INTEGER,
    rw_enabled INTEGER,
    PRIMARY KEY (id)
);

CREATE VIEW texture_view AS
SELECT
    o.*,
    t.width,
    t.height,
    f.name AS format,
    t.mip_count,
    t.rw_enabled
FROM object_view o
INNER JOIN textures t ON o.id = t.id
LEFT JOIN texture_formats f ON t.format = f.id;

INSERT INTO texture_formats (id, name)
VALUES
(0, 'None'),
(1, 'Alpha8'),
(2, 'ARGB4444'),
(3, 'RGB24'),
(4, 'RGBA32'),
(5, 'ARGB32'),
(6, 'ARGBFloat'),
(7, 'RGB565'),
(8, 'BGR24'),
(9, 'AlphaLum16'),
(10, 'DXT1'),
(11, 'DXT3'),
(12, 'DXT5'),
(13, 'RGBA4444'),
(14, 'BGRA32'),
(15, 'RHalf'),
(16, 'RGHalf'),
(17, 'RGBAHalf'),
(18, 'RFloat'),
(19, 'RGFloat'),
(20, 'RGBAFloat'),
(21, 'YUY2'),
(22, 'RGB9e5Float'),
(23, 'RGBFloat'),
(24, 'BC6H'),
(25, 'BC7'),
(26, 'BC4'),
(27, 'BC5'),
(28, 'DXT1Crunched'),
(29, 'DXT5Crunched'),
(30, 'PVRTC_RGB2'),
(31, 'PVRTC_RGBA2'),
(32, 'PVRTC_RGB4'),
(33, 'PVRTC_RGBA4'),
(34, 'ETC_RGB4'),
(35, 'ATC_RGB4'),
(36, 'ATC_RGBA8'),
(41, 'EAC_R'),
(42, 'EAC_R_SIGNED'),
(43, 'EAC_RG'),
(44, 'EAC_RG_SIGNED'),
(45, 'ETC2_RGB'),
(46, 'ETC2_RGBA1'),
(47, 'ETC2_RGBA8'),
(48, 'ASTC_RGB_4x4'),
(49, 'ASTC_RGB_5x5'),
(50, 'ASTC_RGB_6x6'),
(51, 'ASTC_RGB_8x8'),
(52, 'ASTC_RGB_10x10'),
(53, 'ASTC_RGB_12x12'),
(54, 'ASTC_RGBA_4x4'),
(55, 'ASTC_RGBA_5x5'),
(56, 'ASTC_RGBA_6x6'),
(57, 'ASTC_RGBA_8x8'),
(58, 'ASTC_RGBA_10x10'),
(59, 'ASTC_RGBA_12x12'),
(60, 'ETC_RGB4_3DS'),
(61, 'ETC_RGBA8_3DS'),
(62, 'RG16'),
(63, 'R8'),
(64, 'ETC_RGB4Crunched'),
(65, 'ETC2_RGBA8Crunched');