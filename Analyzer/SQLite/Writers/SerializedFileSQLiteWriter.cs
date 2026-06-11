using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands.SerializedFile;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.Util;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Writers;

public class SerializedFileSQLiteWriter : IDisposable
{
    private HashSet<int> m_TypeSet = new();

    private int m_CurrentAssetBundleId = -1;
    private int m_NextAssetBundleId = 0;

    private bool m_SkipReferences;
    private bool m_SkipCrc;

    private IdProvider<string> m_SerializedFileIdProvider = new();
    private ObjectIdProvider m_ObjectIdProvider = new();

    private Regex m_RegexSceneFile = new(@"BuildPlayer-([^\.]+)(?:\.sharedAssets)?");

    // Used to map PPtr fileId to its corresponding serialized file id in the database.
    Dictionary<int, int> m_LocalToDbFileId = new();

    private Dictionary<string, ISQLiteHandler> m_Handlers = new()
    {
        { "Mesh", new MeshHandler() },
        { "Texture2D", new Texture2DHandler() },
        { "Shader", new ShaderHandler() },
        { "AudioClip", new AudioClipHandler() },
        { "AnimationClip", new AnimationClipHandler() },
        { "AssetBundle", new AssetBundleHandler() },
        { "PreloadData", new PreloadDataHandler() },
        { "MonoScript", new MonoScriptHandler() },
        { "BuildReport", new BuildReportHandler() },
        { "PackedAssets", new PackedAssetsHandler() },
    };

    // serialized files
    private AddReference m_AddReferenceCommand = new AddReference();
    private AddAssetBundle m_AddAssetBundleCommand = new AddAssetBundle();
    private AddSerializedFile m_AddSerializedFileCommand = new AddSerializedFile();
    private AddObject m_AddObjectCommand = new AddObject();
    private AddType m_AddTypeCommand = new AddType();
    private AddAssetDependency m_InsertDepCommand = new AddAssetDependency();

    private bool m_Initialized;
    private SqliteConnection m_Database;
    private SqliteCommand m_LastId = new SqliteCommand();
    private SqliteTransaction m_CurrentTransaction = null;
    public SerializedFileSQLiteWriter(SqliteConnection database, bool skipReferences, bool skipCrc)
    {
        m_Initialized = false;
        m_Database = database;
        m_SkipReferences = skipReferences;
        m_SkipCrc = skipCrc;
    }

    public void Init()
    {
        if (m_Initialized)
            return;

        m_Initialized = true;
        foreach (var handler in m_Handlers.Values)
        {
            handler.Init(m_Database);
        }
        CreateSQLiteCommands();
    }

    private void CreateSQLiteCommands()
    {

        // build serialized file commands
        m_AddReferenceCommand.CreateCommand(m_Database);
        m_AddAssetBundleCommand.CreateCommand(m_Database);
        m_AddSerializedFileCommand.CreateCommand(m_Database);
        m_AddObjectCommand.CreateCommand(m_Database);
        m_AddTypeCommand.CreateCommand(m_Database);
        m_InsertDepCommand.CreateCommand(m_Database);

        m_LastId = m_Database.CreateCommand();
        m_LastId.CommandText = "SELECT last_insert_rowid()";
    }
    public void BeginAssetBundle(string name, long size)
    {
        if (m_CurrentAssetBundleId != -1)
        {
            throw new InvalidOperationException("SQLWriter.BeginAssetBundle called twice");
        }

        m_CurrentAssetBundleId = m_NextAssetBundleId++;
        m_AddAssetBundleCommand.SetValue("id", m_CurrentAssetBundleId);
        m_AddAssetBundleCommand.SetValue("name", name);
        m_AddAssetBundleCommand.SetValue("file_size", size);
        m_AddAssetBundleCommand.ExecuteNonQuery();
    }

    public void EndAssetBundle()
    {
        if (m_CurrentAssetBundleId == -1)
        {
            throw new InvalidOperationException("SQLWriter.EndAssetBundle called before SQLWriter.BeginAssetBundle");
        }

        m_CurrentAssetBundleId = -1;
    }

    public void WriteSerializedFile(string relativePath, string fullPath, string containingFolder)
    {
        using var sf = UnityFileSystem.OpenSerializedFile(fullPath);
        using var reader = new UnityFileReader(fullPath, 64 * 1024 * 1024);
        using var pptrReader = new PPtrAndCrcProcessor(sf, reader, containingFolder, m_SkipCrc, AddReference);
        int serializedFileId = m_SerializedFileIdProvider.GetId(Path.GetFileName(fullPath).ToLower());
        int sceneId = -1;

        using var transaction = m_Database.BeginTransaction();
        m_CurrentTransaction = transaction;

        var match = m_RegexSceneFile.Match(relativePath);

        if (match.Success)
        {
            var sceneName = match.Groups[1].Value;

            // There is no Scene object in Unity (a Scene is the full content of a
            // SerializedFile). We generate an object id using the name of the Scene
            // as SerializedFile name, and the object id 0.
            sceneId = m_ObjectIdProvider.GetId((m_SerializedFileIdProvider.GetId(sceneName), 0));

            // There are 2 SerializedFiles per Scene, one ends with .sharedAssets. This is a
            // dirty trick to avoid inserting the scene object a second time.
            if (relativePath.EndsWith(".sharedAssets"))
            {
                m_AddObjectCommand.SetTransaction(transaction);
                m_AddObjectCommand.SetValue("game_object", ""); // or other value
                m_AddObjectCommand.SetValue("id", sceneId);
                m_AddObjectCommand.SetValue("object_id", 0);
                m_AddObjectCommand.SetValue("serialized_file", serializedFileId);
                // The type is set to -1 which doesn't exist in Unity, but is associated to
                // "Scene" in the database.
                m_AddObjectCommand.SetValue("type", -1);
                m_AddObjectCommand.SetValue("name", sceneName);
                m_AddObjectCommand.SetValue("size", 0);
                m_AddObjectCommand.SetValue("crc32", 0);
                m_AddObjectCommand.ExecuteNonQuery();
            }
        }

        m_LocalToDbFileId.Clear();

        Context ctx = new()
        {
            AssetBundleId = m_CurrentAssetBundleId,
            SerializedFileId = serializedFileId,
            SceneId = sceneId,
            ObjectIdProvider = m_ObjectIdProvider,
            SerializedFileIdProvider = m_SerializedFileIdProvider,
            LocalToDbFileId = m_LocalToDbFileId,
            Transaction = transaction,
        };

        ctx.Transaction = transaction;
        try
        {
            m_AddSerializedFileCommand.SetTransaction(transaction);
            m_AddSerializedFileCommand.SetValue("id", serializedFileId);
            m_AddSerializedFileCommand.SetValue("asset_bundle", m_CurrentAssetBundleId == -1 ? "" : m_CurrentAssetBundleId);
            m_AddSerializedFileCommand.SetValue("name", relativePath);
            m_AddSerializedFileCommand.ExecuteNonQuery();

            int localId = 0;
            m_LocalToDbFileId.Add(localId++, serializedFileId);
            foreach (var extRef in sf.ExternalReferences)
            {
                m_LocalToDbFileId.Add(localId++,
                    m_SerializedFileIdProvider.GetId(extRef.Path.Substring(extRef.Path.LastIndexOf('/') + 1).ToLower()));
            }

            foreach (var obj in sf.Objects)
            {
                var currentObjectId = m_ObjectIdProvider.GetId((serializedFileId, obj.Id));
                // Console.WriteLine($"\nProcessing {currentObjectId}");
                var root = sf.GetTypeTreeRoot(obj.Id);
                var offset = obj.Offset;
                uint crc32 = 0;

                if (!m_TypeSet.Contains(obj.TypeId))
                {
                    m_AddTypeCommand.SetTransaction(transaction);
                    m_AddTypeCommand.SetValue("id", obj.TypeId);
                    m_AddTypeCommand.SetValue("name", root.Type);
                    m_AddTypeCommand.ExecuteNonQuery();

                    m_TypeSet.Add(obj.TypeId);
                }

                var randomAccessReader = new RandomAccessReader(sf, root, reader, offset);

                string name = string.Empty;
                long streamDataSize = 0;

                if (m_Handlers.TryGetValue(root.Type, out var handler))
                {
                    handler.Process(ctx, currentObjectId, randomAccessReader,
                        out name, out streamDataSize);
                }
                else if (randomAccessReader.HasChild("m_Name"))
                {
                    name = randomAccessReader["m_Name"].GetValue<string>();
                }

                if (randomAccessReader.HasChild("m_GameObject"))
                {
                    var pptr = randomAccessReader["m_GameObject"];
                    var fileId = m_LocalToDbFileId[pptr["m_FileID"].GetValue<int>()];
                    var gameObjectID = m_ObjectIdProvider.GetId((fileId, pptr["m_PathID"].GetValue<long>()));
                    m_AddObjectCommand.SetValue("game_object", gameObjectID);
                }
                else
                {
                    m_AddObjectCommand.SetValue("game_object", "");
                }

                // The walk both extracts references and accumulates the CRC, so it is needed
                // unless both are disabled. When CRC is on but references are off, the walk
                // still resolves referenced object ids (AddReference skips the insert).
                if (!m_SkipReferences || !m_SkipCrc)
                {
                    crc32 = pptrReader.Process(currentObjectId, offset, root);
                }

                // convert this to the new syntax
                m_AddObjectCommand.SetTransaction(transaction);
                m_AddObjectCommand.SetValue("id", currentObjectId);
                m_AddObjectCommand.SetValue("object_id", obj.Id);
                m_AddObjectCommand.SetValue("serialized_file", serializedFileId);
                m_AddObjectCommand.SetValue("type", obj.TypeId);
                m_AddObjectCommand.SetValue("name", name);
                m_AddObjectCommand.SetValue("size", obj.Size + streamDataSize);
                m_AddObjectCommand.SetValue("crc32", crc32);
                m_AddObjectCommand.ExecuteNonQuery();

                // If this is a Scene AssetBundle, add the object as a depencency of the
                // current scene.
                if (ctx.SceneId != -1)
                {
                    m_InsertDepCommand.SetTransaction(transaction);
                    m_InsertDepCommand.SetValue("object", ctx.SceneId);
                    m_InsertDepCommand.SetValue("dependency", currentObjectId);
                    m_InsertDepCommand.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    // Callback from PPtrAndCrcProcessor for each reference discovered in the SerializedFile
    private int AddReference(long objectId, int fileId, long pathId, string propertyPath, string propertyType)
    {
        // Always resolve the id so the CRC stays stable; only persist the row when references
        // are being extracted.
        var referencedObjectId = m_ObjectIdProvider.GetId((m_LocalToDbFileId[fileId], pathId));

        if (!m_SkipReferences)
        {
            m_AddReferenceCommand.SetTransaction(m_CurrentTransaction);
            m_AddReferenceCommand.SetValue("object", objectId);
            m_AddReferenceCommand.SetValue("referenced_object", referencedObjectId);
            m_AddReferenceCommand.SetValue("property_path", propertyPath);
            m_AddReferenceCommand.SetValue("property_type", propertyType);
            m_AddReferenceCommand.ExecuteNonQuery();
        }

        return referencedObjectId;
    }

    public void Dispose()
    {
        foreach (var handler in m_Handlers.Values)
        {
            handler.Dispose();
        }

        // Serialized file dispose calls
        m_AddAssetBundleCommand.Dispose();
        m_AddSerializedFileCommand.Dispose();
        m_AddReferenceCommand.Dispose();
        m_AddObjectCommand.Dispose();
        m_AddTypeCommand.Dispose();
        m_InsertDepCommand.Dispose();

        m_LastId.Dispose();
    }
}
