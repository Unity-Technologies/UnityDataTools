using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table property_types
    (
        id INTEGER,
        name TEXT,
        PRIMARY KEY (id)
    );
    */
    internal class AddPropertyType : AbstractCommand
    {
        protected override string TableName => "property_types";

        protected override string DDLSource => null;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "name", SqliteType.Text }
        };
    }
}
