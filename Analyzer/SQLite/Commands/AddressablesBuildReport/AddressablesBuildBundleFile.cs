using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_bundle_files
        (
            bundle_id INTEGER,
            build_id INTEGER,
            file_rid INTEGER,
            PRIMARY KEY (bundle_id, build_id, file_rid),
            FOREIGN KEY (bundle_id, build_id) REFERENCES addressables_build_bundles(id, build_id)
        );
    */
    internal class AddressablesBuildBundleFile : AbstractCommand
    {
        protected override string TableName => "addressables_build_bundle_files";

        protected override string DDLSource => Properties.Resources.AddrBuildBundleFiles;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "bundle_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "file_rid", SqliteType.Integer }
        };

        public AddressablesBuildBundleFile()
        {
        }
    }
}

