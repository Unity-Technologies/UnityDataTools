using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    // Table definition: Analyzer/Resources/Init.sql
    internal class AddSerializedFile : AbstractCommand
    {
        protected override string TableName => "serialized_files";

        protected override string DDLSource => null;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "archive", SqliteType.Integer },
            { "name", SqliteType.Text }
        };
    }
}
