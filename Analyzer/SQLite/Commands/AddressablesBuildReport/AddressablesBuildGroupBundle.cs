using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_group_bundles
        (
            group_id INTEGER,
            build_id INTEGER,
            bundle_rid INTEGER,
            PRIMARY KEY (group_id, build_id, bundle_rid),
            FOREIGN KEY (group_id, build_id) REFERENCES addr_build_groups(id, build_id)
        );
    */
    internal class AddressablesBuildGroupBundle : AbstractCommand
    {
        protected override string TableName => "addr_build_group_bundles";

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



