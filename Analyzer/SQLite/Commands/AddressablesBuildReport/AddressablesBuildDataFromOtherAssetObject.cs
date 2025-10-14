using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_data_from_other_asset_objects
        (
            data_from_other_asset_id INTEGER,
            build_id INTEGER,
            asset_type INTEGER,
            component_name TEXT,
            local_identifier_in_file INTEGER,
            object_name TEXT,
            serialized_size INTEGER,
            streamed_size INTEGER,
            PRIMARY KEY (data_from_other_asset_id, build_id, local_identifier_in_file),
            FOREIGN KEY (data_from_other_asset_id, build_id) REFERENCES addr_build_data_from_other_assets(id, build_id)
        );
    */
    internal class AddressablesBuildDataFromOtherAssetObject : AbstractCommand
    {
        protected override string TableName => "addr_build_data_from_other_asset_objects";

        protected override string DDLSource => Properties.Resources.AddrBuildDataFromOtherAssetObjects;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "data_from_other_asset_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "asset_type", SqliteType.Integer },
            { "component_name", SqliteType.Text },
            { "local_identifier_in_file", SqliteType.Integer },
            { "object_name", SqliteType.Text },
            { "serialized_size", SqliteType.Integer },
            { "streamed_size", SqliteType.Integer }
        };

        public AddressablesBuildDataFromOtherAssetObject()
        {
        }
    }
}

