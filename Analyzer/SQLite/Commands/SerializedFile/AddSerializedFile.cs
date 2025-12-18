using UnityDataTools.Analyzer.SQLite.Commands;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table serialized_files
    (
        id INTEGER,
        asset_bundle INTEGER,
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
            { "asset_bundle", SqliteType.Integer },
            { "name", SqliteType.Text }
        };
    }
}
