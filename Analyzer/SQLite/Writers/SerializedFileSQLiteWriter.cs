using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands.SerializedFile;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.Util;
using UnityDataTools.BinaryFormat;
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

    // Global id assignment shared across every serialized file in the database.
    // m_SerializedFileIdProvider maps a serialized file (by lowercased file name) to its
    // serialized_files row id; m_ObjectIdProvider maps a (serialized file id, pathId) pair to
    // its objects row id. See ObjectIdProvider for how cross-file references are resolved.
    private IdProvider<string> m_SerializedFileIdProvider = new();
    private ObjectIdProvider m_ObjectIdProvider = new();

    // The refs table stores ids into these deduplicated string tables instead of repeating the
    // property path/type strings on every row. Ids are assigned lazily and are global across all
    // files; the HashSets track which ids have already had their lookup row written.
    private IdProvider<string> m_PropertyPathIdProvider = new();
    private IdProvider<string> m_PropertyTypeIdProvider = new();
    private HashSet<int> m_PropertyPathSet = new();
    private HashSet<int> m_PropertyTypeSet = new();

    // Detects the SerializedFiles of a scene bundle and captures the scene name.
    // LIMITATION (issue 81): this only matches the BuildPipeline.BuildAssetBundles naming convention
    // ("BuildPlayer-<SceneName>"). It does NOT match scene bundles produced by the Scriptable
    // Build Pipeline / Addressables ("CAB-<hash of scene path>") or the Multi-Process Build
    // Pipeline ("CAB-<scene GUID>"), nor player-build scenes ("level0", "level1", ...), so no
    // synthetic Scene object is created for those. For Scriptable Build Pipeline / Addressables
    // scene bundles that still leaves the assetbundle_assets rows AssetBundleHandler writes
    // dangling (unfixed part of issue 81). Player builds have no AssetBundle object; their
    // PreloadData dependencies are attributed to the PreloadData object itself (PreloadDataHandler).
    private Regex m_RegexSceneFile = new(@"BuildPlayer-([^\.]+)(?:\.sharedAssets)?");

    // Rebuilt for each serialized file: maps a PPtr's local m_FileID (0 = this file, 1..N = an
    // entry in this file's external reference table) to the global serialized file id. A PPtr's
    // (m_FileID, m_PathID) is only meaningful within its own file, so it must be translated
    // through this map before being handed to m_ObjectIdProvider.
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
    private AddPropertyName m_AddPropertyNameCommand = new AddPropertyName();
    private AddPropertyType m_AddPropertyTypeCommand = new AddPropertyType();
    private AddAssetBundle m_AddAssetBundleCommand = new AddAssetBundle();
    private AddSerializedFile m_AddSerializedFileCommand = new AddSerializedFile();
    private AddObject m_AddObjectCommand = new AddObject();
    private AddType m_AddTypeCommand = new AddType();
    private AddPreloadDependency m_InsertDepCommand = new AddPreloadDependency();

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
        m_AddPropertyNameCommand.CreateCommand(m_Database);
        m_AddPropertyTypeCommand.CreateCommand(m_Database);
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
        // A file without TypeTrees can only be opened when its types exactly match this build of
        // UnityFileSystemApi. Handing such a file to the native loader produces misleading version
        // mismatch errors and can crash the process, so detect and reject it up front. The native
        // VFS path here may be a real file or an entry inside a mounted archive.
        using (var detectStream = new UnityFileStream(fullPath))
        {
            if (SerializedFileDetector.IsMissingTypeTrees(detectStream))
                throw new SerializedFileOpenException(fullPath, missingTypeTrees: true);
        }

        using var sf = UnityFileSystem.OpenSerializedFile(fullPath);
        using var reader = new UnityFileReader(fullPath, 64 * 1024 * 1024);
        using var pptrReader = new PPtrAndCrcProcessor(sf, reader, containingFolder, m_SkipCrc, AddReference);
        int serializedFileId = m_SerializedFileIdProvider.GetId(Path.GetFileName(fullPath).ToLower());
        int sceneId = -1;

        using var transaction = m_Database.BeginTransaction();
        m_CurrentTransaction = transaction;

        // A scene has no single Unity object to represent it, yet a scene bundle lists the scene
        // (by its .unity path) as the bundle's asset and other objects/preloads need something to
        // hang off of. So for scene bundles we synthesize one "Scene" object per scene and use it
        // as the target of the assetbundle_assets row (AssetBundleHandler) and of the scene's content and
        // preload dependencies (below and PreloadDataHandler). This only happens when the file
        // name matches m_RegexSceneFile; see its LIMITATION note for the builds this misses (issue 81).
        var match = m_RegexSceneFile.Match(relativePath);

        if (match.Success)
        {
            var sceneName = match.Groups[1].Value;

            // There is no Scene object in Unity (a Scene is the full content of a
            // SerializedFile), so we synthesize one. Treat the scene name as if it were a
            // serialized file name to get a file id, then pair it with pathId 0 to get a
            // stable object id for the scene. AssetBundleHandler builds the scene's object id
            // the same way, so the two agree.
            var sceneFileId = m_SerializedFileIdProvider.GetId(sceneName);
            sceneId = m_ObjectIdProvider.GetId((sceneFileId, 0));

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

            // Local file id 0 is always this file itself; ids 1..N follow the order of the
            // external reference table. Resolve each external reference to its global file id
            // by (lowercased) file name.
            int localId = 0;
            m_LocalToDbFileId.Add(localId++, serializedFileId);
            foreach (var extRef in sf.ExternalReferences)
            {
                m_LocalToDbFileId.Add(localId++,
                    m_SerializedFileIdProvider.GetId(extRef.Path.Substring(extRef.Path.LastIndexOf('/') + 1).ToLower()));
            }

            foreach (var obj in sf.Objects)
            {
                // serializedFileId is already this file's global id, so no LocalToDbFileId
                // translation is needed for the file's own objects; obj.Id is the pathId.
                var currentObjectId = m_ObjectIdProvider.GetId((serializedFileId, obj.Id));
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
            var propertyPathId = GetPropertyPathId(propertyPath);
            var propertyTypeId = GetPropertyTypeId(propertyType);

            m_AddReferenceCommand.SetTransaction(m_CurrentTransaction);
            m_AddReferenceCommand.SetValue("object", objectId);
            m_AddReferenceCommand.SetValue("referenced_object", referencedObjectId);
            m_AddReferenceCommand.SetValue("property_path", propertyPathId);
            m_AddReferenceCommand.SetValue("property_type", propertyTypeId);
            m_AddReferenceCommand.ExecuteNonQuery();
        }

        return referencedObjectId;
    }

    // Resolve a property path/type string to its id, writing the lookup row the first time the
    // string is seen. Called within the current transaction (references are being extracted).
    private int GetPropertyPathId(string propertyPath)
    {
        var id = m_PropertyPathIdProvider.GetId(propertyPath);
        if (m_PropertyPathSet.Add(id))
        {
            m_AddPropertyNameCommand.SetTransaction(m_CurrentTransaction);
            m_AddPropertyNameCommand.SetValue("id", id);
            m_AddPropertyNameCommand.SetValue("name", propertyPath);
            m_AddPropertyNameCommand.ExecuteNonQuery();
        }
        return id;
    }

    private int GetPropertyTypeId(string propertyType)
    {
        var id = m_PropertyTypeIdProvider.GetId(propertyType);
        if (m_PropertyTypeSet.Add(id))
        {
            m_AddPropertyTypeCommand.SetTransaction(m_CurrentTransaction);
            m_AddPropertyTypeCommand.SetValue("id", id);
            m_AddPropertyTypeCommand.SetValue("name", propertyType);
            m_AddPropertyTypeCommand.ExecuteNonQuery();
        }
        return id;
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
        m_AddPropertyNameCommand.Dispose();
        m_AddPropertyTypeCommand.Dispose();
        m_AddObjectCommand.Dispose();
        m_AddTypeCommand.Dispose();
        m_InsertDepCommand.Dispose();

        m_LastId.Dispose();
    }
}
