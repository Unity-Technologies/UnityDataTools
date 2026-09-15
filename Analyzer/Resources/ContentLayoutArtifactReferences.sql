CREATE TABLE IF NOT EXISTS content_layout_artifact_references
(
    -- Direct references between binary artifacts, e.g. a content file to its .resS/.resource data
    -- files. Never cyclical. Content-file-to-content-file edges live in
    -- content_layout_serialized_file_dependencies instead.
    artifact_index INTEGER,           -- references content_layout_binary_artifacts.artifact_index
    referenced_artifact_index INTEGER,
    PRIMARY KEY (artifact_index, referenced_artifact_index)
);
