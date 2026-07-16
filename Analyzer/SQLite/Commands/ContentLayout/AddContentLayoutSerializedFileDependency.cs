using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutSerializedFileDependency : AbstractCommand
    {
        protected override string TableName => "content_layout_serialized_file_dependencies";

        protected override string DDLSource => Properties.Resources.ContentLayoutSerializedFileDependencies;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "serialized_file_index", SqliteType.Integer },
            { "position", SqliteType.Integer },
            { "dependency_index", SqliteType.Integer }
        };
    }
}
