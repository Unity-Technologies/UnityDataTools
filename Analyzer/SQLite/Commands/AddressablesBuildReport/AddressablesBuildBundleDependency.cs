using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_bundle_dependencies
        (
            bundle_id INTEGER,
            build_id INTEGER,
            dependency_rid INTEGER,
            PRIMARY KEY (bundle_id, build_id, dependency_rid),
            FOREIGN KEY (bundle_id, build_id) REFERENCES addressables_build_bundles(id, build_id)
        );
    */
    internal class AddressablesBuildBundleDependency : AbstractCommand
    {
        protected override string TableName => "addressables_build_bundle_dependencies";

        protected override string DDLSource => Properties.Resources.AddrBuildBundleDependencies;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "bundle_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "dependency_rid", SqliteType.Integer }
        };

        public AddressablesBuildBundleDependency()
        {
        }
    }
}
