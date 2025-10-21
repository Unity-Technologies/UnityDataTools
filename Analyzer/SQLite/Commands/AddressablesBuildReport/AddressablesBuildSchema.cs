using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_schemas
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
        protected override string TableName => "addressables_build_schemas";

        protected override string DDLSource => Properties.Resources.AddrBuildSchemas;

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

