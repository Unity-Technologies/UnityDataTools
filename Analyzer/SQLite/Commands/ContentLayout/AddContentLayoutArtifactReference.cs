using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutArtifactReference : AbstractCommand
    {
        protected override string TableName => "content_layout_artifact_references";

        protected override string DDLSource => Properties.Resources.ContentLayoutArtifactReferences;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "artifact_index", SqliteType.Integer },
            { "referenced_artifact_index", SqliteType.Integer }
        };
    }
}
