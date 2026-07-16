-- Scenes referenced from each serialized file (LoadableSceneDependencies in the json).
CREATE TABLE IF NOT EXISTS content_layout_loadable_scene_dependencies
(
    serialized_file_index INTEGER,   -- references content_layout_serialized_files.file_index
    scene_path TEXT                  -- matches content_layout_loadable_scenes.path
);
