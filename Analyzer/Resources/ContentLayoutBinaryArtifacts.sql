-- The artifacts that make up the build output (BinaryArtifacts in the json): the serialized
-- files themselves plus the data files they use (.resS, .resource) and the manifest. This is the
-- standard place to find artifact sizes. When stored as a file the filename is the content hash
-- plus an extension derived from the category (content_layout_binary_artifacts_view adds it).
CREATE TABLE IF NOT EXISTS content_layout_binary_artifacts
(
    artifact_index INTEGER,
    content_hash TEXT,
    category TEXT,              -- 'texture' | 'mesh' | 'audio' | 'video' | 'contentfile' | 'manifest'
    size INTEGER,
    PRIMARY KEY (artifact_index)
);
