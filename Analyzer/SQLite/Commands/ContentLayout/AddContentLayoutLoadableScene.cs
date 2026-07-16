using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutLoadableScene : AbstractCommand
    {
        protected override string TableName => "content_layout_loadable_scenes";

        protected override string DDLSource => Properties.Resources.ContentLayoutLoadableScenes;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "guid", SqliteType.Text },
            { "path", SqliteType.Text },
            { "serialized_file_index", SqliteType.Integer }
        };
    }
}
