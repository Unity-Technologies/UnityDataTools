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
        // name from TypeIdRegistry), so ignore the conflict rather than crashing when both a report
        // and its build output are analyzed together. First insert wins: the name for a given id is
        // the same whether it comes from TypeIdRegistry or the TypeTree, so it does not matter which
        // path runs first (a mismatch would indicate a deeper problem). The case that matters is a
        // type missing from TypeIdRegistry (e.g. an older UnityDataTool reading a newer file); the
        // handler skips those, so the name still arrives here from TypeTree analysis.
        protected override string ConflictClause => "OR IGNORE";

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "name", SqliteType.Text }
        };
    }
}
