CREATE TABLE video_clips
(
    id INTEGER,
    width INTEGER,
    height INTEGER,
    frame_rate REAL,
    frame_count INTEGER,
    PRIMARY KEY (id)
);

CREATE VIEW video_clip_view AS
SELECT
    o.*,
    v.width,
    v.height,
    v.frame_rate,
    v.frame_count
FROM object_view o
INNER JOIN video_clips v ON o.id = v.id
