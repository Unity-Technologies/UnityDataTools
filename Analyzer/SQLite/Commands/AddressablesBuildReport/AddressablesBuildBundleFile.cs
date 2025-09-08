using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_bundle_files
        (
            bundle_id INTEGER,
            build_id INTEGER,
            file_rid INTEGER,
            PRIMARY KEY (bundle_id, build_id, file_rid),
            FOREIGN KEY (bundle_id, build_id) REFERENCES addr_build_bundles(id, build_id)
        );
    */
    internal class AddressablesBuildBundleFile : AbstractCommand
    {
        protected override string TableName => "addr_build_bundle_files";

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

