using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_data_from_other_assets
        (
            id INTEGER,
            build_id INTEGER,
            asset_guid TEXT,
            asset_path TEXT,
            file INTEGER,
            main_asset_type INTEGER,
            object_count INTEGER,
            serialized_size INTEGER,
            streamed_size INTEGER,
            PRIMARY KEY (id, build_id)
        );
    */
    internal class AddressablesBuildDataFromOtherAsset : AbstractCommand
    {
        protected override string TableName => "addr_build_data_from_other_assets";

        protected override string DDLSource => Properties.Resources.AddrBuildDataFromOtherAssets;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "asset_guid", SqliteType.Text },
            { "asset_path", SqliteType.Text },
            { "file", SqliteType.Integer },
            { "main_asset_type", SqliteType.Integer },
            { "object_count", SqliteType.Integer },
            { "serialized_size", SqliteType.Integer },
            { "streamed_size", SqliteType.Integer }
        };

        public AddressablesBuildDataFromOtherAsset()
        {
        }
    }
}
