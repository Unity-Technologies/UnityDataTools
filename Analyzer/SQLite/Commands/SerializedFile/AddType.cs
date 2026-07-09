using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table types
    (
        id INTEGER,
        name TEXT,
        PRIMARY KEY (id)
    );
    */
    internal class AddType : AbstractCommand
    {
        protected override string TableName => "types";

        protected override string DDLSource => null;

        // The BuildReport PackedAssetsHandler may have already inserted a type by numeric id (with a
        // name from TypeIdRegistry). The TypeTree name here is authoritative and identical for known
        // ids, so ignore the conflict rather than crashing when both a report and its build output
        // are analyzed together.
        protected override string ConflictClause => "OR IGNORE";

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "name", SqliteType.Text }
        };
    }
}
