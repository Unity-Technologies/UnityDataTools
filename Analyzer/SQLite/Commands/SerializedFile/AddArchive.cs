using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.SerializedFile
{
    // Table definition: Analyzer/Resources/Init.sql
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
