using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_file_assets
        (
            file_id INTEGER,
            build_id INTEGER,
            asset_rid INTEGER,
            PRIMARY KEY (file_id, build_id, asset_rid),
            FOREIGN KEY (file_id, build_id) REFERENCES addressables_build_files(id, build_id)
        );
    */
    internal class AddressablesBuildFileAsset : AbstractCommand
    {
        protected override string TableName => "addressables_build_file_assets";

        protected override string DDLSource => Properties.Resources.AddrBuildFileAssets;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "file_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "asset_rid", SqliteType.Integer }
        };

        public AddressablesBuildFileAsset()
        {
        }
    }
}


