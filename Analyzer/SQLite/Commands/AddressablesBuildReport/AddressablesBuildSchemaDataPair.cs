using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    /* TABLE DEFINITION:
        create table addressables_build_schema_data_pairs
        (
            schema_id INTEGER,
            build_id INTEGER,
            key TEXT,
            value TEXT,
            PRIMARY KEY (schema_id, build_id, key),
            FOREIGN KEY (schema_id, build_id) REFERENCES addressables_build_schemas(id, build_id)
        );
    */
    internal class AddressablesBuildSchemaDataPair : AbstractCommand
    {
        protected override string TableName => "addressables_build_schema_data_pairs";

        protected override string DDLSource => Properties.Resources.AddrBuildSchemaDataPairs;

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "schema_id", SqliteType.Integer },
            { "build_id", SqliteType.Integer },
            { "key", SqliteType.Text },
            { "value", SqliteType.Text }
        };

        public AddressablesBuildSchemaDataPair()
        {
        }
    }
}
