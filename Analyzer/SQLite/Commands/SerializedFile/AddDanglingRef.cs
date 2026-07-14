using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table dangling_refs
    (
        id INTEGER,
        object_id INTEGER,
        serialized_file INTEGER,
        PRIMARY KEY (id)
    );
    */
    internal class AddDanglingRef : AbstractCommand
    {
        protected override string TableName => "dangling_refs";

        protected override string DDLSource => null;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "object_id", SqliteType.Integer },
            { "serialized_file", SqliteType.Integer }
        };
    }
}
