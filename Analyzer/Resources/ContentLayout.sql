-- Identity of the imported ContentLayout.json (see Documentation/contentlayout.md). The
-- content_layout* tables are only created when a ContentLayout.json is part of the analyzed
-- input. A single layout per database is supported.
CREATE TABLE IF NOT EXISTS content_layout
(
    id INTEGER,                 -- always 0 (single layout per database)
    name TEXT,                  -- path of the imported ContentLayout.json
    version INTEGER,            -- schema version of the json file
    build_manifest_hash TEXT,
    PRIMARY KEY (id)
);
