-- Direct references between binary artifacts (ArtifactReferences in the json), e.g. a
-- serialized file referencing its .resS/.resource data files. References that go through a
-- loadable are not included, and the graph is never cyclical. References to other serialized
-- files are not recorded here either; those are in content_layout_serialized_file_dependencies.
CREATE TABLE IF NOT EXISTS content_layout_artifact_references
(
    artifact_index INTEGER,           -- references content_layout_binary_artifacts.artifact_index
    referenced_artifact_index INTEGER,
    PRIMARY KEY (artifact_index, referenced_artifact_index)
);
