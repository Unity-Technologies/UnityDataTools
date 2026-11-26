using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    // Table definition: Analyzer/Resources/Init.sql
    internal class AddStreamData : AbstractCommand
    {
        protected override string TableName => "stream_data";

        protected override string DDLSource => null;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "object", SqliteType.Integer },
            { "property_path", SqliteType.Text },
            { "path", SqliteType.Text },
            { "offset", SqliteType.Integer },
            { "size", SqliteType.Integer }
        };
    }
}
