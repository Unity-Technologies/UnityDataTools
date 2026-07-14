using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

// Processes the AssetBundle Unity object, which only exists in content built as AssetBundles.
// It fills the assetbundle_assets table from the object's m_Container (the bundle's explicit
// assets) and adds their dependencies from m_PreloadTable. Player and ContentDirectory builds
// have no AssetBundle object, so this handler never runs for them (preload_dependencies can still
// get rows from other sources there - see AssetBundle.sql).
//
// Scenes have no single Unity object, so for scene bundles we synthesize one "Scene" object per
// scene and point the assetbundle_assets row at it. The scene object is keyed on the scene's
// SerializedFile so that PreloadDataHandler (processing that scene's .sharedAssets file) resolves
// the same id and attaches the scene's preload dependencies to it - see SerializedFileSQLiteWriter.
public class AssetBundleHandler : ISQLiteHandler
{
    SqliteCommand m_InsertCommand;
    private SqliteCommand m_InsertDepCommand;
    private SqliteCommand m_InsertSceneObjectCommand;

    // Scene object ids already inserted, so a scene referenced by more than one bundle is not
    // inserted twice (objects.id is a primary key). Persists across files for the whole run.
    private HashSet<long> m_InsertedSceneObjects = new();

    // Legacy fallback for BuildPipeline.BuildAssetBundles scene bundles, which do not emit
    // m_SceneHashes: extracts the scene name from a container entry like "Assets/Foo/Scene1.unity".
    // The Scene object for those is created by SerializedFileSQLiteWriter (keyed on this same name).
    private Regex m_SceneNameRegex = new Regex(@"([^//]+)\.unity");

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Resources.AssetBundle;
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();

        m_InsertCommand.CommandText = "INSERT INTO assetbundle_assets(object, name) VALUES(@object, @name)";
        m_InsertCommand.Parameters.Add("@object", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@name", SqliteType.Text);

        m_InsertDepCommand = db.CreateCommand();

        m_InsertDepCommand.CommandText = "INSERT INTO preload_dependencies(object, dependency) VALUES(@object, @dependency)";
        m_InsertDepCommand.Parameters.Add("@object", SqliteType.Integer);
        m_InsertDepCommand.Parameters.Add("@dependency", SqliteType.Integer);

        // Synthetic Scene object (type -1 = "Scene"): object_id 0, no game_object/size/crc.
        m_InsertSceneObjectCommand = db.CreateCommand();
        m_InsertSceneObjectCommand.CommandText =
            "INSERT INTO objects(id, object_id, serialized_file, type, name, game_object, size, crc32) " +
            "VALUES(@id, 0, @serialized_file, -1, @name, '', 0, 0)";
        m_InsertSceneObjectCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertSceneObjectCommand.Parameters.Add("@serialized_file", SqliteType.Integer);
        m_InsertSceneObjectCommand.Parameters.Add("@name", SqliteType.Text);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var assetBundle = AssetBundle.Read(reader);

        foreach (var asset in assetBundle.Assets)
        {
            if (!assetBundle.IsSceneAssetBundle)
            {
                // Editor-only container entries (e.g. ShaderSubGraph, .preset) can appear in
                // m_Container with a null PPtr (m_FileID 0 and m_PathID 0) because they have no
                // runtime object. Skip them: resolving the null PPtr would allocate a phantom
                // (file, 0) object id and record a bogus dangling reference to it.
                if (asset.PPtr.FileId == 0 && asset.PPtr.PathId == 0)
                    continue;

                var fileId = ctx.LocalToDbFileId[asset.PPtr.FileId];
                var objId = ctx.ObjectIdProvider.GetId((fileId, asset.PPtr.PathId));
                m_InsertCommand.Transaction = ctx.Transaction;
                m_InsertCommand.Parameters["@object"].Value = objId;
                m_InsertCommand.Parameters["@name"].Value = asset.Name;
                m_InsertCommand.ExecuteNonQuery();

                for (int i = asset.PreloadIndex; i < asset.PreloadIndex + asset.PreloadSize; ++i)
                {
                    var dependency = assetBundle.PreloadTable[i];
                    var depFileId = ctx.LocalToDbFileId[dependency.FileId];
                    var depId = ctx.ObjectIdProvider.GetId((depFileId, dependency.PathId));
                    m_InsertDepCommand.Transaction = ctx.Transaction;
                    m_InsertDepCommand.Parameters["@object"].Value = objId;
                    m_InsertDepCommand.Parameters["@dependency"].Value = depId;
                    m_InsertDepCommand.ExecuteNonQuery();
                }
            }
            else if (assetBundle.SceneToFile.TryGetValue(asset.Name, out var sceneFile))
            {
                // Scriptable Build Pipeline / Addressables: key the scene on its SerializedFile
                // (from m_SceneHashes) and create the synthetic Scene object here, since the writer
                // cannot recognise a "CAB-<hash>" scene file by name.
                var sceneFileId = ctx.SerializedFileIdProvider.GetId(sceneFile.ToLowerInvariant());
                var objId = ctx.ObjectIdProvider.GetId((sceneFileId, 0));

                // The synthetic Scene object is inserted once (objects.id is a primary key), but the
                // container entry is always recorded, so a scene exposed by more than one bundle
                // still gets its assetbundle_assets row.
                if (m_InsertedSceneObjects.Add(objId))
                {
                    m_InsertSceneObjectCommand.Transaction = ctx.Transaction;
                    m_InsertSceneObjectCommand.Parameters["@id"].Value = objId;
                    m_InsertSceneObjectCommand.Parameters["@serialized_file"].Value = sceneFileId;
                    m_InsertSceneObjectCommand.Parameters["@name"].Value = asset.Name;
                    m_InsertSceneObjectCommand.ExecuteNonQuery();
                }

                m_InsertCommand.Transaction = ctx.Transaction;
                m_InsertCommand.Parameters["@object"].Value = objId;
                m_InsertCommand.Parameters["@name"].Value = asset.Name;
                m_InsertCommand.ExecuteNonQuery();
            }
            else
            {
                // Legacy BuildPipeline.BuildAssetBundles: the Scene object is created by
                // SerializedFileSQLiteWriter (keyed on the scene name); here we add only the asset row.
                var match = m_SceneNameRegex.Match(asset.Name);

                if (match.Success)
                {
                    var sceneName = match.Groups[1].Value;
                    var objId = ctx.ObjectIdProvider.GetId((ctx.SerializedFileIdProvider.GetId(sceneName), 0));
                    m_InsertCommand.Transaction = ctx.Transaction;
                    m_InsertCommand.Parameters["@object"].Value = objId;
                    m_InsertCommand.Parameters["@name"].Value = asset.Name;
                    m_InsertCommand.ExecuteNonQuery();
                }
            }
        }

        name = assetBundle.Name;
        streamDataSize = 0;
    }

    public void Finalize(SqliteConnection db)
    {
        using var command = new SqliteCommand();
        command.Connection = db;
        command.CommandText = "CREATE INDEX preload_dependencies_object ON preload_dependencies(object)";
        command.ExecuteNonQuery();

        command.CommandText = "CREATE INDEX preload_dependencies_dependency ON preload_dependencies(dependency)";
        command.ExecuteNonQuery();
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
        m_InsertDepCommand?.Dispose();
        m_InsertSceneObjectCommand?.Dispose();
    }
}
