using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutLoadableSceneDependency : AbstractCommand
    {
        protected override string TableName => "content_layout_loadable_scene_dependencies";

        protected override string DDLSource => Properties.Resources.ContentLayoutLoadableSceneDependencies;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "serialized_file_index", SqliteType.Integer },
            { "scene_path", SqliteType.Text }
        };
    }
}
