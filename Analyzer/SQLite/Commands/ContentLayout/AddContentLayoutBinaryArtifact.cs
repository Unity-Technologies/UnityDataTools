using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutBinaryArtifact : AbstractCommand
    {
        protected override string TableName => "content_layout_binary_artifacts";

        protected override string DDLSource => Properties.Resources.ContentLayoutBinaryArtifacts;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "artifact_index", SqliteType.Integer },
            { "content_hash", SqliteType.Text },
            { "category", SqliteType.Text },
            { "size", SqliteType.Integer }
        };
    }
}
