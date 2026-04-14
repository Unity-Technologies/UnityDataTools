using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_group_bundles
        (
            group_id INTEGER,
            build_id INTEGER,
            bundle_rid INTEGER,
            PRIMARY KEY (group_id, build_id, bundle_rid),
            FOREIGN KEY (group_id, build_id) REFERENCES addressables_build_groups(id, build_id)
        );
    */
    internal class AddressablesBuildGroupBundle : AbstractCommand
    {
        protected override string TableName => "addressables_build_group_bundles";

        protected override string DDLSource => Properties.Resources.AddrBuildGroupBundles;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "group_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "bundle_rid", SqliteType.Integer }
        };

        public AddressablesBuildGroupBundle()
        {
        }
    }
}

