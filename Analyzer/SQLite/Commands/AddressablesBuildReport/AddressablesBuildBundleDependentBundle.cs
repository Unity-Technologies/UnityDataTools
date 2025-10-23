using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_bundle_dependent_bundles
        (
            bundle_id INTEGER,
            build_id INTEGER,
            dependent_bundle_rid INTEGER,
            PRIMARY KEY (bundle_id, build_id, dependent_bundle_rid),
            FOREIGN KEY (bundle_id, build_id) REFERENCES addressables_build_bundles(id, build_id)
        );
    */
    internal class AddressablesBuildBundleDependentBundle : AbstractCommand
    {
        protected override string TableName => "addressables_build_bundle_dependent_bundles";

        protected override string DDLSource => Properties.Resources.AddrBuildBundleDependentBundles;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "bundle_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "dependent_bundle_rid", SqliteType.Integer }
        };

        public AddressablesBuildBundleDependentBundle()
        {
        }
    }
}

