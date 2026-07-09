using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table serialized_files
    (
        id INTEGER,
        archive INTEGER,
        name TEXT,
        PRIMARY KEY (id)
    );
    */
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
