using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_bundle_regular_dependencies
        (
            bundle_id INTEGER,
            build_id INTEGER,
            dependency_rid INTEGER,
            PRIMARY KEY (bundle_id, build_id, dependency_rid),
            FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
        );
    */
    internal class AddressablesBuildBundleRegularDependency : AbstractCommand
    {
        protected override string TableName => "addr_build_bundle_regular_dependencies";

        protected override string DDLSource => Properties.Resources.AddrBuildBundleRegularDependencies;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "bundle_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "dependency_rid", SqliteType.Integer }
        };

        public AddressablesBuildBundleRegularDependency()
        {
        }
    }
}

