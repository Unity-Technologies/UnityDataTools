using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands
{
    /* TABLE DEFINITION:
        create table addr_build_bundles
        (
            id INTEGER,
            build_id INTEGER,
            asset_count INTEGER,
            build_status INTEGER,
            crc INTEGER,
            compression TEXT,
            dependency_file_size INTEGER,
            expanded_dependency_file_size INTEGER,
            file_size INTEGER,
            group_rid INTEGER,
            hash TEXT,
            internal_name TEXT,
            load_path TEXT,
            name TEXT,
            provider TEXT,
            result_type TEXT,
            PRIMARY KEY (id, build_id)
        );
    */
    internal class AddressablesBuildBundle : AbstractCommand
    {
        protected override string TableName => "addr_build_bundles";

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "asset_count", SqliteType.Integer },
            { "build_status", SqliteType.Integer },
            { "crc", SqliteType.Integer },
            { "compression", SqliteType.Text },
            { "dependency_file_size", SqliteType.Integer },
            { "expanded_dependency_file_size", SqliteType.Integer },
            { "file_size", SqliteType.Integer },
            { "group_rid", SqliteType.Integer },
            { "hash", SqliteType.Text }, // JSON object stored as TEXT
            { "internal_name", SqliteType.Text },
            { "load_path", SqliteType.Text },
            { "name", SqliteType.Text },
            { "provider", SqliteType.Text },
            { "result_type", SqliteType.Text }
        };

        public AddressablesBuildBundle()
        {
        }
    }
}
