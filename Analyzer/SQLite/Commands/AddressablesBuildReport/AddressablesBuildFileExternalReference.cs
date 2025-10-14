using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_file_external_references
        (
            file_id INTEGER,
            build_id INTEGER,
            external_reference_rid INTEGER,
            PRIMARY KEY (file_id, build_id, external_reference_rid),
            FOREIGN KEY (file_id, build_id) REFERENCES addr_build_files(id, build_id)
        );
    */
    internal class AddressablesBuildFileExternalReference : AbstractCommand
    {
        protected override string TableName => "addr_build_file_external_references";

        protected override string DDLSource => Properties.Resources.AddrBuildFileExternalReferences;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "file_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "external_reference_rid", SqliteType.Integer }
        };

        public AddressablesBuildFileExternalReference()
        {
        }
    }
}


