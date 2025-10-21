CREATE TABLE IF NOT EXISTS addressables_build_data_from_other_asset_object_references
(
    data_from_other_asset_id INTEGER,
    build_id INTEGER,
    local_identifier_in_file INTEGER,
    asset_id INTEGER,
    object_id INTEGER,
    PRIMARY KEY (data_from_other_asset_id, build_id, local_identifier_in_file, asset_id, object_id),
    FOREIGN KEY (data_from_other_asset_id, build_id, local_identifier_in_file) REFERENCES addressables_build_data_from_other_asset_objects(data_from_other_asset_id, build_id, local_identifier_in_file)
);