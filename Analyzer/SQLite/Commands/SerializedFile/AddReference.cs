using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    // Table definition: Analyzer/Resources/Init.sql
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
