using Analyzer.SQLite.Commands;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table types
    (
        id INTEGER,
        name TEXT,
        PRIMARY KEY (id)
    );
    */
    internal class AddType : AbstractCommand
    {
        protected override string TableName => "types";

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "name", SqliteType.Text }
        };
    }
}