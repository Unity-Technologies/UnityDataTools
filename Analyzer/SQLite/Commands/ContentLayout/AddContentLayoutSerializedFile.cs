using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutSerializedFile : AbstractCommand
    {
        protected override string TableName => "content_layout_serialized_files";

        protected override string DDLSource => Properties.Resources.ContentLayoutSerializedFiles;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "file_index", SqliteType.Integer },
            { "cfid", SqliteType.Text },
            { "is_builtin", SqliteType.Integer },
            { "content_hash", SqliteType.Text },
            { "serialized_file", SqliteType.Integer }
        };
    }
}
