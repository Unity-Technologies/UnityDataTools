using System;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;


namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class MonoScriptHandler : ISQLiteHandler
{
    SqliteCommand m_InsertCommand;

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Resources.MonoScript;
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = "INSERT INTO monoscripts(id, class_name, namespace, assembly_name) VALUES(@id, @class_name, @namespace, @assembly_name)";
        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@class_name", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@namespace", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@assembly_name", SqliteType.Text);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var monoScript = MonoScript.Read(reader);
        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@class_name"].Value = monoScript.ClassName;
        m_InsertCommand.Parameters["@namespace"].Value = monoScript.Namespace;
        m_InsertCommand.Parameters["@assembly_name"].Value = monoScript.AssemblyName;
        m_InsertCommand.ExecuteNonQuery();

        name = monoScript.ClassName;
        streamDataSize = 0;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
    }
}
