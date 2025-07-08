using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands
{
    /* TABLE DEFINITION:
        create table addr_build_file_sub_files
        (
            file_id INTEGER,
            build_id INTEGER,
            sub_file_rid INTEGER,
            PRIMARY KEY (file_id, build_id, sub_file_rid),
            FOREIGN KEY (file_id, build_id) REFERENCES addr_build_files(id, build_id)
        );
    */
    internal class AddressablesBuildFileSubFile : AbstractCommand
    {
        protected override string TableName => "addr_build_file_sub_files";

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


