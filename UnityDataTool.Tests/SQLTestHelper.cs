using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

/// <summary>
/// Helper methods for executing SQL queries against a DB created by "Analyze"
/// and validating results in tests.
/// </summary>
public static class SQLTestHelper
{
    /// <summary>
    /// Default database filename used in tests.
    /// </summary>
    public const string DefaultDatabaseName = "database.db";

    /// <summary>
    /// Creates and opens a SQLite database connection with standard test settings.
    /// </summary>
    /// <param name="databasePath">The path to the database file.</param>
    /// <returns>An opened SqliteConnection. Caller is responsible for disposing.</returns>
    public static SqliteConnection OpenDatabase(string databasePath)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            ForeignKeys = false,
        }.ConnectionString);
        db.Open();
        return db;
    }

    /// <summary>
    /// Gets the standard database path for tests (testOutputFolder/database.db).
    /// </summary>
    /// <param name="testOutputFolder">The test output folder path.</param>
    /// <returns>The full path to the database file.</returns>
    public static string GetDatabasePath(string testOutputFolder)
    {
        return Path.Combine(testOutputFolder, DefaultDatabaseName);
    }

    /// <summary>
    /// Executes a SQL query and returns the integer result.
    /// </summary>
    /// <param name="db">The database connection to use.</param>
    /// <param name="sql">The SQL query to execute (should return a single integer value).</param>
    /// <returns>The integer result of the query.</returns>
    public static int QueryInt(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return reader.GetInt32(0);
    }

    /// <summary>
    /// Executes a SQL query and returns the string result.
    /// </summary>
    /// <param name="db">The database connection to use.</param>
    /// <param name="sql">The SQL query to execute (should return a single string value).</param>
    /// <returns>The string result of the query.</returns>
    public static string QueryString(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return reader.GetString(0);
    }

    /// <summary>
    /// Executes a SQL query and asserts the result equals the expected integer value.
    /// </summary>
    /// <param name="db">The database connection to use.</param>
    /// <param name="sql">The SQL query to execute (should return a single integer value).</param>
    /// <param name="expectedValue">The expected integer result.</param>
    /// <param name="description">Description of what is being tested (used in assertion message).</param>
    public static void AssertQueryInt(SqliteConnection db, string sql, int expectedValue, string description)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.AreEqual(expectedValue, reader.GetInt32(0), description);
    }

    /// <summary>
    /// Executes a SQL query and asserts the result equals the expected string value.
    /// </summary>
    /// <param name="db">The database connection to use.</param>
    /// <param name="sql">The SQL query to execute (should return a single string value).</param>
    /// <param name="expectedValue">The expected string result.</param>
    /// <param name="description">Description of what is being tested (used in assertion message).</param>
    public static void AssertQueryString(SqliteConnection db, string sql, string expectedValue, string description)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        Assert.AreEqual(expectedValue, reader.GetString(0), description);
    }
}
