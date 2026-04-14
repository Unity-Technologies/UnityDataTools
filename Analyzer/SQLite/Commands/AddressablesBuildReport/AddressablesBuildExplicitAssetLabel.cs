using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_explicit_asset_labels
        (
            explicit_asset_id INTEGER,
            build_id INTEGER,
            label TEXT,
            PRIMARY KEY (explicit_asset_id, build_id, label),
            FOREIGN KEY (explicit_asset_id, build_id) REFERENCES addressables_build_explicit_assets(id, build_id)
        );
    */
    internal class AddressablesBuildExplicitAssetLabel : AbstractCommand
    {
        protected override string TableName => "addressables_build_explicit_asset_labels";

        protected override string DDLSource => Properties.Resources.AddrBuildExplicitAssetLabels;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "explicit_asset_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "label", SqliteType.Text }
        };

        public AddressablesBuildExplicitAssetLabel()
        {
        }
    }
}

