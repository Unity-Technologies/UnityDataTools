using System;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class AnimationClipHandler : SQLiteHandlerBase
{
    SqliteCommand m_InsertCommand;

    public override void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Properties.Resources.AnimationClip;
        command.ExecuteNonQuery();
        
        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = "INSERT INTO animation_clips(id, legacy, events) VALUES(@id, @legacy, @events)";
        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@legacy", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@events", SqliteType.Integer);
    }

    public override void ProcessObject(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var animationClip = AnimationClip.Read(reader);
        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@legacy"].Value = animationClip.Legacy;
        m_InsertCommand.Parameters["@events"].Value = animationClip.Events;
        m_InsertCommand.ExecuteNonQuery();

        name = animationClip.Name;
        streamDataSize = 0;
    }

    public override void Dispose()
    {
        m_InsertCommand?.Dispose();
    }
}