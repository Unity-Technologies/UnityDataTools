using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands
{
    /* TABLE DEFINITION:
        create table addr_build_files
        (
            id INTEGER,
            build_id INTEGER,
            assets TEXT,
            bundle INTEGER,
            bundle_object_info_size INTEGER,
            external_references TEXT,
            mono_script_count INTEGER,
            mono_script_size INTEGER,
            name TEXT,
            other_assets TEXT,
            preload_info_size INTEGER,
            sub_files TEXT,
            write_result_filename TEXT,
            PRIMARY KEY (id, build_id)
        );
    */
    internal class AddressablesBuildFile : AbstractCommand
    {
        protected override string TableName => "addr_build_files";

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "assets", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "bundle", SqliteType.Integer },
            { "bundle_object_info_size", SqliteType.Integer },
            { "external_references", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "mono_script_count", SqliteType.Integer },
            { "mono_script_size", SqliteType.Integer },
            { "name", SqliteType.Text },
            { "other_assets", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "preload_info_size", SqliteType.Integer },
            { "sub_files", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "write_result_filename", SqliteType.Text }
        };

        public AddressablesBuildFile()
        {
        }
    }
}
