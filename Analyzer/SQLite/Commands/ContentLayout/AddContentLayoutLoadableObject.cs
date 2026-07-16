using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutLoadableObject : AbstractCommand
    {
        protected override string TableName => "content_layout_loadable_objects";

        protected override string DDLSource => Properties.Resources.ContentLayoutLoadableObjects;

        protected override Dictionary<string, SqliteType> Fields => new()
        {
            { "object_id_hash", SqliteType.Text },
            { "guid", SqliteType.Text },
            { "asset_path", SqliteType.Text },
            { "lfid", SqliteType.Integer },
            { "identifier_type", SqliteType.Integer },
            { "serialized_file_index", SqliteType.Integer },
            { "output_lfid", SqliteType.Integer },
            { "is_root_asset", SqliteType.Integer }
        };
    }
}
