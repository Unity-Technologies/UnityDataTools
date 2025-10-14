using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_sub_files
        (
            id INTEGER,
            build_id INTEGER,
            is_serialized_file INTEGER,
            name TEXT,
            size INTEGER,
            PRIMARY KEY (id, build_id)
        );
    */
    internal class AddressablesBuildSubFile : AbstractCommand
    {
        protected override string TableName => "addr_build_sub_files";

        protected override string DDLSource => Properties.Resources.AddrBuildSubFiles;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "is_serialized_file", SqliteType.Integer },
            { "name", SqliteType.Text },
            { "size", SqliteType.Integer }
        };

        public AddressablesBuildSubFile()
        {
        }
    }
}
