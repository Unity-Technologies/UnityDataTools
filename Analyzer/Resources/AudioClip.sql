CREATE TABLE audio_load_types(
    id INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE audio_formats(
    id INTEGER,
    name TEXT,
    PRIMARY KEY (id)
);

CREATE TABLE audio_clips(
    id INTEGER,
    bits_per_sample INTEGER,
    frequency INTEGER,
    channels INTEGER,
    load_type INTEGER,
    format INTEGER,
    PRIMARY KEY (id)
);

CREATE VIEW audio_clip_view AS
SELECT
	o.*,
	a.bits_per_sample,
	a.frequency,
	a.channels,
	l.name AS load_type,
	f.name AS format
FROM object_view o
INNER JOIN audio_clips a ON o.id = a.id
LEFT JOIN audio_load_types l ON a.load_type = l.id
LEFT JOIN audio_formats f ON a.format = f.id;

INSERT INTO audio_load_types (id, name)
VALUES
(0, 'Decompress on Load'),
(1, 'Compressed in Memory'),
(2, 'Streaming');

INSERT INTO audio_formats (id, name)
VALUES
(0, 'PCM'),
(1, 'Vorbis'),
(2, 'ADPCM'),
(3, 'MP3'),
(4, 'PSMVAG'),
(5, 'HEVAG'),
(6, 'XMA'),
(7, 'AAC'),
(8, 'GCADPCM'),
(9, 'ATRAC9');
