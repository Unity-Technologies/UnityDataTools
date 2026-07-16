using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutSourceAsset : AbstractCommand
    {
        protected override string TableName => "content_layout_source_assets";

        protected override string DDLSource => Properties.Resources.ContentLayoutSourceAssets;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "serialized_file_index", SqliteType.Integer },
            { "asset_path", SqliteType.Text }
        };
    }
}
