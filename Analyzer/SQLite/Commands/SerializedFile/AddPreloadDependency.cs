using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    // Table definition: Analyzer/Resources/AssetBundle.sql
    internal class AddPreloadDependency : AbstractCommand
    {
        protected override string TableName => "preload_dependencies";

        protected override string DDLSource => Resources.AssetBundle;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "object", SqliteType.Integer },
            { "dependency", SqliteType.Integer }
        };
    }
}
