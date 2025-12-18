using UnityDataTools.Analyzer.SQLite.Commands;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table asset_dependencies
    (
        object INTEGER,
        dependency INTEGER,
        PRIMARY KEY (object, dependency)
    );
    */
    internal class AddAssetDependency : AbstractCommand
    {
        protected override string TableName => "asset_dependencies";

        protected override string DDLSource => Resources.AssetBundle;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "object", SqliteType.Integer },
            { "dependency", SqliteType.Integer }
        };
    }
}
