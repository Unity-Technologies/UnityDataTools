using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_file_sub_files
        (
            file_id INTEGER,
            build_id INTEGER,
            sub_file_rid INTEGER,
            PRIMARY KEY (file_id, build_id, sub_file_rid),
            FOREIGN KEY (file_id, build_id) REFERENCES addressables_build_files(id, build_id)
        );
    */
    internal class AddressablesBuildFileSubFile : AbstractCommand
    {
        protected override string TableName => "addressables_build_file_sub_files";

        protected override string DDLSource => Properties.Resources.AddrBuildFileSubFiles;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "file_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "sub_file_rid", SqliteType.Integer }
        };

        public AddressablesBuildFileSubFile()
        {
        }
    }
}


