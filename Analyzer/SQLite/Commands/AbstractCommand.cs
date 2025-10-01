using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace Analyzer.SQLite.Commands
{
    internal abstract class AbstractCommand
    {
        protected abstract string TableName { get; }
        protected abstract Dictionary<string, SqliteType> Fields { get; }

        private SqliteCommand m_Command = new SqliteCommand();

        protected virtual string DDLSource { get => null; }

        // run data definition language commands to create
        // tables and views, run once at the beginning of creating
        // the database
        public void RunDDL(SqliteConnection database)
        {
            if (DDLSource == null)
                return;
            using var command = database.CreateCommand();
            command.CommandText = DDLSource;
            command.ExecuteNonQuery();
        }

        public void CreateCommand(SqliteConnection database)
        {
            RunDDL(database);

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
