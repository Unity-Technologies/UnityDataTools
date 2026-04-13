using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.Analyzer.SQLite.Writers
{
    public class SQLiteWriter : IDisposable
    {
        private SqliteConnection m_Database;
        public SqliteConnection Connection => m_Database;
        private string m_DatabaseName;


        public SQLiteWriter(string databaseName)
        {
            m_DatabaseName = databaseName;
        }

        public void Begin()
        {
            if (m_Database != null)
            {
                throw new InvalidOperationException("SQLiteWriter.Begin called twice");
            }
            SqliteConnectionStringBuilder builder = new();
            builder.DataSource = m_DatabaseName;
            builder.Mode = SqliteOpenMode.ReadWriteCreate;
            m_Database = new SqliteConnection(builder.ConnectionString);
            File.WriteAllBytes(m_DatabaseName, Array.Empty<byte>());
            try
            {
                m_Database.Open();

                using var walCommand = m_Database.CreateCommand();
                walCommand.CommandText = "PRAGMA journal_mode=WAL";
                walCommand.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error creating database: {e.Message}");
            }

            using var command = m_Database.CreateCommand();
            command.CommandText = Resources.Init;
            command.ExecuteNonQuery();
        }

        public void End()
        {
            if (m_Database == null)
            {
                throw new InvalidOperationException("SQLiteWriter.End called before SQLiteWriter.Begin");
            }

            using var finalizeCommand = m_Database.CreateCommand();
            finalizeCommand.CommandText = Resources.Finalize;
            finalizeCommand.ExecuteNonQuery();
        }

        public void Dispose()
        {
            m_Database?.Dispose();
            m_Database = null;
        }
    }
}
