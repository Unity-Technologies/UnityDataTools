using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

/// <summary>
/// Helper methods for executing SQL queries and validating results in tests.
/// </summary>
public static class SQLTestHelper
{
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
