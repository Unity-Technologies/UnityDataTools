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
        property_path INTEGER,   -- id into property_names
        property_type INTEGER    -- id into property_types
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
            { "property_path", SqliteType.Integer },
            { "property_type", SqliteType.Integer }
        };
    }
}
