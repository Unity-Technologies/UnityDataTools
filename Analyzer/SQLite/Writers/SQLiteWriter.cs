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
            // A pooled connection keeps the file open after Dispose, which blocks deleting it.
            builder.Pooling = false;
            m_Database = new SqliteConnection(builder.ConnectionString);
            File.WriteAllBytes(m_DatabaseName, Array.Empty<byte>());
            try
            {
                m_Database.Open();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error creating database: {e.Message}");
            }

            // this does all the legacy import of Init.sql
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

        // Closes the database and deletes its file, for a run whose result is not worth keeping.
        public void Discard()
        {
            Dispose();
            File.Delete(m_DatabaseName);
        }

        public void Dispose()
        {
            m_Database?.Dispose();
            m_Database = null;
        }
    }
}
