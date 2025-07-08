using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands
{
    /* TABLE DEFINITION:
        create table addr_build_schemas
        (
            id INTEGER,
            build_id INTEGER,
            guid TEXT,
            type TEXT,
            PRIMARY KEY (id, build_id)
        );
    */
    internal class AddressablesBuildSchema : AbstractCommand
    {
        protected override string TableName => "addr_build_schemas";

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "guid", SqliteType.Text },
            { "type", SqliteType.Text }
        };

        public AddressablesBuildSchema()
        {
        }
    }
}

