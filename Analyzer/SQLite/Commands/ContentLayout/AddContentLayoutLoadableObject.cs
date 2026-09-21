using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands.ContentLayout
{
    internal class AddContentLayoutLoadableObject : AbstractCommand
    {
        // Version 2 layouts (Unity 6.6) record the source asset path and source lfid of each
        // loadable; newer versions do not. The table is created with those columns only when a
        // v2 layout is imported, so v3 databases are not cluttered with always-NULL columns.
        // Set before CreateCommand, which reads DDLSource and Fields.
        public bool V2Columns { get; set; }

        protected override string TableName => "content_layout_loadable_objects";

        protected override string DDLSource => V2Columns
            ? Properties.Resources.ContentLayoutLoadableObjectsV2
            : Properties.Resources.ContentLayoutLoadableObjects;

        protected override Dictionary<string, SqliteType> Fields
        {
            get
            {
                var fields = new Dictionary<string, SqliteType>
                {
                    { "loadable_index", SqliteType.Integer },
                    { "guid", SqliteType.Text },
                    { "lfid", SqliteType.Integer },
                    { "identifier_type", SqliteType.Integer },
                    { "serialized_file_index", SqliteType.Integer },
                    { "is_root_asset", SqliteType.Integer }
                };

                if (V2Columns)
                {
                    fields.Add("asset_path", SqliteType.Text);
                    fields.Add("source_lfid", SqliteType.Integer);
                }

                return fields;
            }
        }
    }
}
