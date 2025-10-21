using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_group_schemas
        (
            group_id INTEGER,
            build_id INTEGER,
            schema_rid INTEGER,
            PRIMARY KEY (group_id, build_id, schema_rid),
            FOREIGN KEY (group_id, build_id) REFERENCES addressables_build_groups(id, build_id)
        );
    */
    internal class AddressablesBuildGroupSchema : AbstractCommand
    {
        protected override string TableName => "addressables_build_group_schemas";

        protected override string DDLSource => Properties.Resources.AddrBuildGroupSchemas;

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



