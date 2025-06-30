using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Analyzer.SQLite.Commands
{
    internal class AddressablesBuild : AbstractCommand
    {
        protected override string TableName { get => "addr_builds"; }
        protected override Dictionary<string, SqliteType> Fields { get => new Dictionary<string, SqliteType>
        {
            { "name", SqliteType.Text },
            { "build_target", SqliteType.Integer },
            { "start_time", SqliteType.Integer },
            { "duration", SqliteType.Real },
            { "error", SqliteType.Text },
            { "package_version", SqliteType.Text },
            { "player_version", SqliteType.Text },
            { "build_script", SqliteType.Text },
            { "result_hash", SqliteType.Text },
            { "type", SqliteType.Integer },
            { "unity_version", SqliteType.Text }
        }; }
        public AddressablesBuild()
        {

        }


    }
}
