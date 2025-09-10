using Analyzer.SQLite.Commands;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table objects
    (
        id INTEGER,
        object_id INTEGER,
        serialized_file INTEGER,
        type INTEGER,
        name TEXT,
        game_object INTEGER,
        size INTEGER,
        crc32 INTEGER,
        PRIMARY KEY (id)
    );
    */
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