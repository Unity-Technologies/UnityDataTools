using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_file_other_assets
        (
            file_id INTEGER,
            build_id INTEGER,
            other_asset_rid INTEGER,
            PRIMARY KEY (file_id, build_id, other_asset_rid),
            FOREIGN KEY (file_id, build_id) REFERENCES addr_build_files(id, build_id)
        );
    */
    internal class AddressablesBuildFileOtherAsset : AbstractCommand
    {
        protected override string TableName => "addr_build_file_other_assets";

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "file_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "other_asset_rid", SqliteType.Integer }
        };

        public AddressablesBuildFileOtherAsset()
        {
        }
    }
}


