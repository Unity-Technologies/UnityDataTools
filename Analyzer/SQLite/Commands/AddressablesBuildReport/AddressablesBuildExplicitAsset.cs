using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_explicit_assets
        (
            id INTEGER,
            build_id INTEGER,
            bundle INTEGER,
            file INTEGER,
            asset_hash TEXT,
            asset_path TEXT,
            addressable_name TEXT,
            group_guid TEXT,
            guid TEXT,
            internal_id TEXT,
            main_asset_type INTEGER,
            serialized_size INTEGER,
            streamed_size INTEGER,
            PRIMARY KEY (id, build_id)
        );
    */
    internal class AddressablesBuildExplicitAsset : AbstractCommand
    {
        protected override string TableName => "addr_build_explicit_assets";

        protected override string DDLSource => Properties.Resources.AddrBuildExplicitAssets;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer},
            { "bundle", SqliteType.Integer},
            { "file", SqliteType.Integer },
            { "asset_hash", SqliteType.Text },
            { "asset_path", SqliteType.Text },
            { "addressable_name", SqliteType.Text },
            { "group_guid", SqliteType.Text },
            { "guid", SqliteType.Text },
            { "internal_id", SqliteType.Text },
            { "main_asset_type", SqliteType.Integer },
            { "streamed_size", SqliteType.Integer },
            { "serialized_size", SqliteType.Integer }
        };
        public AddressablesBuildExplicitAsset()
        {
        }
    }
}
