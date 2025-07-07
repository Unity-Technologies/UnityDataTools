using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands
{
    /* TABLE DEFINITION:
        create table addr_build_groups
        (
            id INTEGER,
            build_id INTEGER,
            bundles TEXT,
            guid TEXT,
            name TEXT,
            packing_mode TEXT,
            schemas TEXT,
            PRIMARY KEY (id, build_id)
        );
    */
    internal class AddressablesBuildGroup : AbstractCommand
    {
        protected override string TableName => "addr_build_groups";

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "bundles", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "guid", SqliteType.Text },
            { "name", SqliteType.Text },
            { "packing_mode", SqliteType.Text },
            { "schemas", SqliteType.Text } // JSONB type in SQLite uses TEXT
        };

        public AddressablesBuildGroup()
        {
        }
    }
}

