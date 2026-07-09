using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Commands
{
    internal abstract class AbstractCommand
    {
        protected abstract string TableName { get; }
        protected abstract Dictionary<string, SqliteType> Fields { get; }

        private SqliteCommand m_Command = new SqliteCommand();

        protected virtual string DDLSource { get => null; }

        // Conflict-resolution clause for the INSERT (e.g. "OR IGNORE"). Defaults to none, so a
        // duplicate primary key surfaces as an error - some tables rely on that to detect problems
        // such as the same SerializedFile being analyzed twice.
        protected virtual string ConflictClause => "";

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
            var insert = string.IsNullOrEmpty(ConflictClause) ? "INSERT" : $"INSERT {ConflictClause}";
            var commandText = new StringBuilder($"{insert} INTO {TableName} (");
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
