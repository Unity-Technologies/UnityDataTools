CREATE TABLE animation_clips(
    id INTEGER,
    legacy INTEGER,
    events INTEGER,
    PRIMARY KEY (id)
);

CREATE VIEW animation_view AS
SELECT
    o.*,
    a.legacy,
    a.events
FROM object_view o INNER JOIN animation_clips a ON o.id = a.id;
