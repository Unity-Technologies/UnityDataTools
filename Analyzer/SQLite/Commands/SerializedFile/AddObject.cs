using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    // Table definition: Analyzer/Resources/Init.sql
    internal class AddObject : AbstractCommand
    {
        protected override string TableName => "objects";

        protected override string DDLSource => null;
        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "object_id", SqliteType.Integer },
            { "serialized_file", SqliteType.Integer },
            { "type", SqliteType.Integer },
            { "name", SqliteType.Text },
            { "game_object", SqliteType.Integer },
            { "size", SqliteType.Integer },
            { "crc32", SqliteType.Integer }
        };
    }
}
