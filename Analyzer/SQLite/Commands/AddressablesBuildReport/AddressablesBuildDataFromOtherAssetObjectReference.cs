using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_data_from_other_asset_object_references
        (
            data_from_other_asset_id INTEGER,
            build_id INTEGER,
            local_identifier_in_file INTEGER,
            asset_id INTEGER,
            object_id INTEGER,
            PRIMARY KEY (data_from_other_asset_id, build_id, local_identifier_in_file, asset_id, object_id),
            FOREIGN KEY (data_from_other_asset_id, build_id, local_identifier_in_file) REFERENCES addr_build_data_from_other_asset_objects(data_from_other_asset_id, build_id, local_identifier_in_file)
        );
    */
    internal class AddressablesBuildDataFromOtherAssetObjectReference : AbstractCommand
    {
        protected override string TableName => "addr_build_data_from_other_asset_object_references";

        protected override string DDLSource => Properties.Resources.AddrBuildDataFromOtherAssetObjectReferences;
        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "data_from_other_asset_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "local_identifier_in_file", SqliteType.Integer },
            { "asset_id", SqliteType.Integer },
            { "object_id", SqliteType.Integer }
        };

        public AddressablesBuildDataFromOtherAssetObjectReference()
        {
        }
    }
}
