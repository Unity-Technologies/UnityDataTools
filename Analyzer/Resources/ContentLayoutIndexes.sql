-- Created after the content_layout tables are populated, so inserts stay fast for very large
-- layouts. The content_hash and asset_path indexes carry the views; the rest serve reverse
-- lookups ("who depends on X", "which loadables live in file Y").
CREATE INDEX content_layout_binary_artifacts_content_hash ON content_layout_binary_artifacts(content_hash);
CREATE INDEX content_layout_source_assets_asset_path ON content_layout_source_assets(asset_path);
CREATE INDEX content_layout_source_assets_file ON content_layout_source_assets(serialized_file_index);
CREATE INDEX content_layout_serialized_file_dependencies_dep ON content_layout_serialized_file_dependencies(dependency_index);
CREATE INDEX content_layout_artifact_references_ref ON content_layout_artifact_references(referenced_artifact_index);
CREATE INDEX content_layout_loadable_objects_file ON content_layout_loadable_objects(serialized_file_index);
CREATE INDEX content_layout_loadable_objects_guid ON content_layout_loadable_objects(guid);
