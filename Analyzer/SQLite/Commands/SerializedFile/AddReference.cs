using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table refs
    (
        object INTEGER,
        referenced_object INTEGER, 
        property_path TEXT,
        property_type TEXT,
        PRIMARY KEY (object, referenced_object, property_path)
    );
    */
    internal class AddReference : AbstractCommand
    {
        protected override string TableName => "refs";

        protected override string DDLSource => null;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "object", SqliteType.Integer },
            { "referenced_object", SqliteType.Integer },
            { "property_path", SqliteType.Text },
            { "property_type", SqliteType.Text }
        };
    }
}
