using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands
{
    /* TABLE DEFINITION:
        create table addr_build_data_from_other_asset_referencing_assets
        (
            data_from_other_asset_id INTEGER,
            build_id INTEGER,
            referencing_asset_rid INTEGER,
            PRIMARY KEY (data_from_other_asset_id, build_id, referencing_asset_rid),
            FOREIGN KEY (data_from_other_asset_id, build_id) REFERENCES addr_build_data_from_other_assets(id, build_id)
        );
    */
    internal class AddressablesBuildDataFromOtherAssetReferencingAsset : AbstractCommand
    {
        protected override string TableName => "addr_build_data_from_other_asset_referencing_assets";

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "data_from_other_asset_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "referencing_asset_rid", SqliteType.Integer }
        };

        public AddressablesBuildDataFromOtherAssetReferencingAsset()
        {
        }
    }
}

