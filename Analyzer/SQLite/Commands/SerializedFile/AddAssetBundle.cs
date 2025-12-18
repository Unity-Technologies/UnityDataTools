using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table asset_bundles
    (
        id INTEGER,
        name TEXT,
        file_size INTEGER,
        PRIMARY KEY (id)
    );
    */
    internal class AddAssetBundle : AbstractCommand
    {
        protected override string TableName => "asset_bundles";

        protected override string DDLSource => null;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "name", SqliteType.Text },
            { "file_size", SqliteType.Integer }
        };
    }
}
