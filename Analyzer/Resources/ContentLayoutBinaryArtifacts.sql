CREATE TABLE IF NOT EXISTS content_layout_binary_artifacts
(
    -- Every artifact making up the build output: the content files plus the data files they use
    -- (.resS, .resource) and the manifest. The standard place to find artifact sizes.
    artifact_index INTEGER,
    content_hash TEXT,          -- the on-disk filename is content_hash plus an extension from category
    category TEXT,              -- 'texture' | 'mesh' | 'audio' | 'video' | 'contentfile' | 'manifest'
    size INTEGER,
    PRIMARY KEY (artifact_index)
);
