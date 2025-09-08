using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzer.SQLite.Commands
{
    internal abstract class AbstractCommand
    {
        protected abstract string TableName { get; }
        protected abstract Dictionary<string, SqliteType> Fields { get; }

        private SqliteCommand m_Command = new SqliteCommand();

        public void CreateCommand(SqliteConnection database)
        {
            m_Command = database.CreateCommand();
            var commandText = new StringBuilder($"INSERT INTO {TableName} (");
            commandText.Append(string.Join(", ", Fields.Keys));
            commandText.Append(") VALUES (@");
            commandText.Append(string.Join(", @", Fields.Keys));
            commandText.Append(")");
            m_Command.CommandText = commandText.ToString();

            foreach (var entry in Fields)
            {
                m_Command.Parameters.Add("@" + entry.Key, entry.Value);
            }
        }
        public void SetValue(string key, object value)
        {
            string prefixedKey = $"@{key}";
            if (m_Command.Parameters.Contains(prefixedKey))
            {
                m_Command.Parameters[prefixedKey].Value = value ?? DBNull.Value;
            }
            else
            {
                throw new ArgumentException($"Parameter '{key}' does not exist in the command.");
            }
        }

        public void SetTransaction(SqliteTransaction transaction)
        {
            m_Command.Transaction = transaction;
        }

        public int ExecuteNonQuery()
        {
            return m_Command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            m_Command?.Dispose();
        }
    }
}
