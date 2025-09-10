using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_files
        (
            id INTEGER,
            build_id INTEGER,
            bundle INTEGER,
            bundle_object_info_size INTEGER,
            mono_script_count INTEGER,
            mono_script_size INTEGER,
            name TEXT,
            preload_info_size INTEGER,
            write_result_filename TEXT,
            PRIMARY KEY (id, build_id)
        );
    */
    internal class AddressablesBuildFile : AbstractCommand
    {
        protected override string TableName => "addr_build_files";

        protected override string DDLSource => Properties.Resources.AddrBuildFiles;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "bundle", SqliteType.Integer },
            { "bundle_object_info_size", SqliteType.Integer },
            { "mono_script_count", SqliteType.Integer },
            { "mono_script_size", SqliteType.Integer },
            { "name", SqliteType.Text },
            { "preload_info_size", SqliteType.Integer },
            { "write_result_filename", SqliteType.Text }
        };

        public AddressablesBuildFile()
        {
        }
    }
}
