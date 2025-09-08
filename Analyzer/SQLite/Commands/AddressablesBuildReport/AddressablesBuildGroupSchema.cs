using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addr_build_group_schemas
        (
            group_id INTEGER,
            build_id INTEGER,
            schema_rid INTEGER,
            PRIMARY KEY (group_id, build_id, schema_rid),
            FOREIGN KEY (group_id, build_id) REFERENCES addr_build_groups(id, build_id)
        );
    */
    internal class AddressablesBuildGroupSchema : AbstractCommand
    {
        protected override string TableName => "addr_build_group_schemas";

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "group_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "schema_rid", SqliteType.Integer }
        };

        public AddressablesBuildGroupSchema()
        {
        }
    }
}



