using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzer.SQLite.Commands
{
    internal class AddressablesBuildExplicitAsset : AbstractCommand
    {
        protected override string TableName => "addr_build_explicit_assets";

        protected override Dictionary<string, SqliteType> Fields => new Dictionary<string, SqliteType>
        {
            { "id", SqliteType.Integer },
            { "build_id", SqliteType.Integer},
            { "bundle", SqliteType.Integer},
            { "file", SqliteType.Integer },
            { "asset_hash", SqliteType.Text },
            { "asset_path", SqliteType.Text },
            { "addressable_name", SqliteType.Text },
            { "externally_referenced_assets", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "group_guid", SqliteType.Text },
            { "guid", SqliteType.Text },
            { "internal_id", SqliteType.Text },
            { "internal_referenced_explicit_assets", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "internal_referenced_other_assets", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "labels", SqliteType.Text }, // JSONB type in SQLite uses TEXT
            { "streamed_size", SqliteType.Integer },
            { "serialized_size", SqliteType.Integer },
            { "main_asset_type", SqliteType.Integer }
        };
        public AddressablesBuildExplicitAsset()
        {
        }
    }
}
