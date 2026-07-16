using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutLoadableDependency : AbstractCommand
    {
        protected override string TableName => "content_layout_loadable_dependencies";

        protected override string DDLSource => Properties.Resources.ContentLayoutLoadableDependencies;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "serialized_file_index", SqliteType.Integer },
            { "object_id_hash", SqliteType.Text }
        };
    }
}
