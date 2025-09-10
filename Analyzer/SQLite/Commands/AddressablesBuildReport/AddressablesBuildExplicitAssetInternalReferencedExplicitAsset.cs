using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_explicit_asset_internal_referenced_explicit_assets
        (
            explicit_asset_id INTEGER,
            build_id INTEGER,
            internal_referenced_explicit_asset_rid INTEGER,
            PRIMARY KEY (explicit_asset_id, build_id, internal_referenced_explicit_asset_rid),
            FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addr_build_explicit_assets(id, build_id)
        );
    */
    internal class AddressablesBuildExplicitAssetInternalReferencedExplicitAsset : AbstractCommand
    {
        protected override string TableName => "addr_build_explicit_asset_internal_referenced_explicit_assets";

        protected override string DDLSource => Properties.Resources.AddrBuildExplicitAssetInternalReferencedExplicitAssets;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "explicit_asset_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "internal_referenced_explicit_asset_rid", SqliteType.Integer }
        };

        public AddressablesBuildExplicitAssetInternalReferencedExplicitAsset()
        {
        }
    }
}

