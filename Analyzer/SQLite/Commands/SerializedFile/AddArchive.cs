using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    /* TABLE DEFINITION:
    create table archives
    (
        id INTEGER,
        name TEXT,
        file_size INTEGER,
        PRIMARY KEY (id)
    );
    */
    internal class AddArchive : AbstractCommand
    {
        protected override string TableName => "archives";

        protected override string DDLSource => null;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "name", SqliteType.Text },
            { "file_size", SqliteType.Integer }
        };
    }
}
