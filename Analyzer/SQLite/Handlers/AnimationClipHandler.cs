using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class AnimationClipHandler : ISQLiteHandler
{
    SQLiteCommand m_InsertCommand;

    public void Init(SQLiteConnection db)
    {
        using var command = new SQLiteCommand(db);

        command.CommandText = Properties.Resources.AnimationClip;
        command.ExecuteNonQuery();

        m_InsertCommand = new SQLiteCommand(db);
        m_InsertCommand.CommandText = "INSERT INTO animation_clips(id, legacy, events) VALUES(@id, @legacy, @events)";
        m_InsertCommand.Parameters.Add("@id", DbType.Int64);
        m_InsertCommand.Parameters.Add("@legacy", DbType.Int32);
        m_InsertCommand.Parameters.Add("@events", DbType.Int32);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var animationClip = AnimationClip.Read(reader);

        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@legacy"].Value = animationClip.Legacy;
        m_InsertCommand.Parameters["@events"].Value = animationClip.Events;
        m_InsertCommand.ExecuteNonQuery();

        name = animationClip.Name;
        streamDataSize = 0;
    }

    public void Finalize(SQLiteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand.Dispose();
    }
}