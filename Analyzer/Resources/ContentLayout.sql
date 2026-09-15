CREATE TABLE IF NOT EXISTS content_layout
(
    -- Identity of the imported ContentLayout.json. The content_layout* tables exist only when a
    -- layout was part of the analyzed input. See Documentation/contentlayout-database.md.
    id INTEGER,                 -- always 0 (single layout per database)
    name TEXT,                  -- path of the imported ContentLayout.json
    version INTEGER,            -- schema version of the json file
    build_manifest_hash TEXT,
    PRIMARY KEY (id)
);
