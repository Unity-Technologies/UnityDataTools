using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayout : AbstractCommand
    {
        protected override string TableName => "content_layout";

        protected override string DDLSource => Properties.Resources.ContentLayout;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "id", SqliteType.Integer },
            { "name", SqliteType.Text },
            { "version", SqliteType.Integer },
            { "build_manifest_hash", SqliteType.Text }
        };
    }
}
