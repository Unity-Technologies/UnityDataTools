using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_groups
        (
            id INTEGER,
            build_id INTEGER,
            guid TEXT,
            name TEXT,
            packing_mode TEXT,
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
            { "guid", SqliteType.Text },
            { "name", SqliteType.Text },
            { "packing_mode", SqliteType.Text },
        };

        public AddressablesBuildGroup()
        {
        }
    }
}

