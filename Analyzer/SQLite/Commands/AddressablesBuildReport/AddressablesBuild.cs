using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport
{
    internal class AddressablesBuild : AbstractCommand
    {
        protected override string TableName { get => "addressables_builds"; }

        protected override string DDLSource => Properties.Resources.AddrBuilds;
        protected override Dictionary<string, SqliteType> Fields { get => new Dictionary<string, SqliteType>
        {
            { "name", SqliteType.Text },
            { "build_target", SqliteType.Integer },
            { "start_time", SqliteType.Text },
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
