using System;
using System.Data;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

// Processes the PreloadData object, recording its m_Assets list as preload_dependencies. The
// "object" the dependencies hang off of depends on the build:
//   * Scene bundle: the synthetic Scene object (ctx.SceneId), which also aggregates the scene's
//     own content.
//   * Player build: there is no scene object (ctx.SceneId == -1), so the dependencies are hung off
//     the PreloadData object itself. A player build has one PreloadData per scene (in its
//     sharedassetsN.assets) plus one in globalgamemanagers.assets for the always-loaded set.
// A dependency may resolve to an object that analyze never tracked (e.g. objects in
// "unity default resources", which normally has no TypeTrees), leaving a row whose dependency has
// no objects-table entry. ContentDirectory builds have no PreloadData object.
public class PreloadDataHandler : ISQLiteHandler
{
    private SqliteCommand m_InsertDepCommand;

    public void Init(SqliteConnection db)
    {
        m_InsertDepCommand = db.CreateCommand();
        m_InsertDepCommand.Connection = db;
        m_InsertDepCommand.CommandText = "INSERT INTO preload_dependencies(object, dependency) VALUES(@object, @dependency)";
        m_InsertDepCommand.Parameters.Add("@object", SqliteType.Integer);
        m_InsertDepCommand.Parameters.Add("@dependency", SqliteType.Integer);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var preloadData = PreloadData.Read(reader);
        m_InsertDepCommand.Transaction = ctx.Transaction;
        // Scene bundles hang the dependencies off the synthetic Scene object; player builds have
        // none, so they hang off the PreloadData object itself.
        m_InsertDepCommand.Parameters["@object"].Value = ctx.SceneId != -1 ? ctx.SceneId : objectId;

        foreach (var asset in preloadData.Assets)
        {
            var fileId = ctx.LocalToDbFileId[asset.FileId];
            var objId = ctx.ObjectIdProvider.GetId((fileId, asset.PathId));

            m_InsertDepCommand.Parameters["@dependency"].Value = objId;
            m_InsertDepCommand.ExecuteNonQuery();
        }

        name = "";
        streamDataSize = 0;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertDepCommand?.Dispose();
    }
}
