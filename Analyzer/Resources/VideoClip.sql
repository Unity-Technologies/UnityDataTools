CREATE TABLE IF NOT EXISTS video_clips(
    -- one row per VideoClip; its video data is in a .resource file and counted in objects.size
    id INTEGER,
    width INTEGER,
    height INTEGER,
    frame_rate REAL,
    frame_count INTEGER,
    audio_tracks INTEGER,
    audio_languages TEXT, -- language of each audio track, comma separated in track order; '' with no audio
    original_path TEXT, -- the source asset path in the project
    PRIMARY KEY (id)
);

CREATE VIEW video_clip_view AS
SELECT
    -- VideoClips with their dimensions, frame rate and length
    o.*,
    v.width,
    v.height,
    v.frame_rate,
    v.frame_count,
    CASE WHEN v.frame_rate > 0 THEN v.frame_count / v.frame_rate END AS duration,
    v.audio_tracks,
    v.audio_languages,
    v.original_path
FROM object_view o
INNER JOIN video_clips v ON o.id = v.id;
