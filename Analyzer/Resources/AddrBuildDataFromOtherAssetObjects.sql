CREATE TABLE IF NOT EXISTS addressables_build_data_from_other_asset_objects
(
    data_from_other_asset_id INTEGER,
    build_id INTEGER,
    asset_type INTEGER,
    component_name TEXT,
    local_identifier_in_file INTEGER,
    object_name TEXT,
    serialized_size INTEGER,
    streamed_size INTEGER,
    PRIMARY KEY (data_from_other_asset_id, build_id, local_identifier_in_file),
    FOREIGN KEY (data_from_other_asset_id, build_id) REFERENCES addressables_build_data_from_other_assets(id, build_id)
);