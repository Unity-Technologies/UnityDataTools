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

    private int m_CurrentArchiveId = -1;
    private int m_NextArchiveId = 0;

    // Names/ids already written to the archives and serialized_files tables, used to reject a
    // second copy of the same content with a clear error instead of a raw UNIQUE constraint
    // failure. Only a single build can be analyzed at a time (see AnalyzeDuplicateException).
    // Archive names are compared case-sensitively, matching the archives.name schema constraint
    // and the name as it exists on the file system.
    private HashSet<string> m_WrittenArchiveNames = new();
    private HashSet<int> m_WrittenSerializedFileIds = new();

    private bool m_SkipReferences;
    private bool m_SkipCrc;

    // Global id assignment shared across every serialized file in the database.
    // m_SerializedFileIdProvider maps a serialized file (by lowercased file name) to its
    // serialized_files row id; it is owned by AnalyzerTool because the ContentLayout import
    // assigns ids through the same provider. m_ObjectIdProvider maps a (serialized file id,
    // pathId) pair to its objects row id. See ObjectIdProvider for how cross-file references
    // are resolved.
    private IdProvider<string> m_SerializedFileIdProvider;
    private ObjectIdProvider m_ObjectIdProvider = new();

    // File-to-file dependencies from an imported ContentLayout.json, used to resolve the
    // external references of ContentDirectory files (whose external reference table holds
    // symbolic placeholders). Empty when no layout is imported.
    private ContentFileDependencyMap m_ContentFileDependencies;

    // The refs table stores ids into these deduplicated string tables instead of repeating the
    // property path/type strings on every row. Ids are assigned lazily and are global across all
    // files; the HashSets track which ids have already had their lookup row written.
    private IdProvider<string> m_PropertyPathIdProvider = new();
    private IdProvider<string> m_PropertyTypeIdProvider = new();
    private HashSet<int> m_PropertyPathSet = new();
    private HashSet<int> m_PropertyTypeSet = new();

    // Detects the SerializedFiles of a BuildPipeline.BuildAssetBundles scene bundle
    // ("BuildPlayer-<SceneName>") and captures the scene name. Scene bundles from the Scriptable
    // Build Pipeline / Addressables instead use "CAB-<hash>" / "CAB-<hash>.sharedAssets" and are
    // handled by the .sharedAssets branch in WriteSerializedFile plus AssetBundleHandler.
    // Player-build scenes ("level0", ...) have no scene object; their PreloadData dependencies are
    // attributed to the PreloadData object itself (PreloadDataHandler).
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
    private AddArchive m_AddArchiveCommand = new AddArchive();
    private AddSerializedFile m_AddSerializedFileCommand = new AddSerializedFile();
    private AddObject m_AddObjectCommand = new AddObject();
    private AddType m_AddTypeCommand = new AddType();
    private AddPreloadDependency m_InsertDepCommand = new AddPreloadDependency();
    private AddDanglingRef m_AddDanglingRefCommand = new AddDanglingRef();

    private bool m_Initialized;
    private SqliteConnection m_Database;
    private SqliteCommand m_LastId = new SqliteCommand();
    private SqliteTransaction m_CurrentTransaction = null;
    public SerializedFileSQLiteWriter(SqliteConnection database, bool skipReferences, bool skipCrc,
        IdProvider<string> serializedFileIdProvider, ContentFileDependencyMap contentFileDependencies)
    {
        m_Initialized = false;
        m_Database = database;
        m_SkipReferences = skipReferences;
        m_SkipCrc = skipCrc;
        m_SerializedFileIdProvider = serializedFileIdProvider;
        m_ContentFileDependencies = contentFileDependencies;
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
        m_AddArchiveCommand.CreateCommand(m_Database);
        m_AddSerializedFileCommand.CreateCommand(m_Database);
        m_AddObjectCommand.CreateCommand(m_Database);
        m_AddTypeCommand.CreateCommand(m_Database);
        m_InsertDepCommand.CreateCommand(m_Database);
        m_AddDanglingRefCommand.CreateCommand(m_Database);

        m_LastId = m_Database.CreateCommand();
        m_LastId.CommandText = "SELECT last_insert_rowid()";
    }
    public void BeginArchive(string name, long size)
    {
        if (m_CurrentArchiveId != -1)
        {
            throw new InvalidOperationException("SQLWriter.BeginArchive called twice");
        }

        // Assign the id before the duplicate check throws, so the caller's EndArchive (called from
        // its finally) unwinds cleanly instead of masking the exception.
        m_CurrentArchiveId = m_NextArchiveId++;

        // Reject a duplicate archive name so no second archives row is created and the caller can
        // report it before reading the archive's contents.
        if (!m_WrittenArchiveNames.Add(name))
        {
            throw new AnalyzeDuplicateException(name, isArchive: true);
        }

        m_AddArchiveCommand.SetValue("id", m_CurrentArchiveId);
        m_AddArchiveCommand.SetValue("name", name);
        m_AddArchiveCommand.SetValue("file_size", size);
        m_AddArchiveCommand.ExecuteNonQuery();
    }

    public void EndArchive()
    {
        if (m_CurrentArchiveId == -1)
        {
            throw new InvalidOperationException("SQLWriter.EndArchive called before SQLWriter.BeginArchive");
        }

        m_CurrentArchiveId = -1;
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
        int serializedFileId = m_SerializedFileIdProvider.GetId(Path.GetFileName(fullPath));
        int sceneId = -1;

        // Two SerializedFiles with the same name map to the same id (the provider deduplicates by
        // name), so a second one would collide on serialized_files.id. Reject it before opening a
        // transaction; the file name is what matters to the user, not the analyzer id.
        if (m_WrittenSerializedFileIds.Contains(serializedFileId))
        {
            throw new AnalyzeDuplicateException(Path.GetFileName(fullPath), isArchive: false);
        }

        using var transaction = m_Database.BeginTransaction();
        m_CurrentTransaction = transaction;

        // A scene has no single Unity object to represent it, yet a scene bundle lists the scene
        // (by its .unity path) as the bundle's asset and its preloads need something to hang off
        // of. So for scene bundles we synthesize one "Scene" object per scene, keyed on the
        // scene's SerializedFile so every place that references it computes the same id. It is the
        // target of the assetbundle_assets row (AssetBundleHandler) and of the scene's preload
        // dependencies (the content loop below and PreloadDataHandler).
        var match = m_RegexSceneFile.Match(relativePath);

        if (match.Success)
        {
            // BuildPipeline.BuildAssetBundles scene bundle. The scene name comes from the file name
            // itself, and the scene object is keyed on that name (AssetBundleHandler matches it
            // from the "<sceneName>.unity" container entry).
            var sceneName = match.Groups[1].Value;
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
        else if (Path.GetFileName(fullPath).EndsWith(".sharedAssets", StringComparison.OrdinalIgnoreCase))
        {
            // Scriptable Build Pipeline / Addressables scene bundle: a scene's shared-assets file is
            // "<sceneFile>.sharedAssets" (e.g. "CAB-<hash>.sharedAssets"). Resolve the scene object
            // id from the scene file name so the content loop below and PreloadDataHandler attach
            // the scene's dependencies to it. Unlike the BuildPipeline case above, the Scene object
            // itself (and its readable name) is created by AssetBundleHandler, which gets the scene
            // path -> file mapping from the AssetBundle object's m_SceneHashes.
            var fileName = Path.GetFileName(fullPath);
            var sceneFileName = fileName.Substring(0, fileName.Length - ".sharedAssets".Length);
            var sceneFileId = m_SerializedFileIdProvider.GetId(sceneFileName);
            sceneId = m_ObjectIdProvider.GetId((sceneFileId, 0));
        }

        m_LocalToDbFileId.Clear();

        Context ctx = new()
        {
            ArchiveId = m_CurrentArchiveId,
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
            m_AddSerializedFileCommand.SetValue("archive", m_CurrentArchiveId == -1 ? "" : m_CurrentArchiveId);
            m_AddSerializedFileCommand.SetValue("name", relativePath);
            m_AddSerializedFileCommand.ExecuteNonQuery();

            // Local file id 0 is always this file itself; ids 1..N follow the order of the
            // external reference table. Resolve each external reference to its global file id
            // by file name (matched case-sensitively, as it appears on disk / in the archive).
            // For ContentDirectory files the external table holds symbolic placeholders, so when
            // an imported ContentLayout covers this file the actual target comes from its
            // dependency list, matched by position; built-in dependencies (null entries) keep the
            // external table path.
            var resolvedDependencies = m_ContentFileDependencies.GetDependencies(
                Path.GetFileName(fullPath));

            if (resolvedDependencies != null && resolvedDependencies.Length != sf.ExternalReferences.Count)
            {
                Console.Error.WriteLine(
                    $"Warning: {relativePath}: the external reference count ({sf.ExternalReferences.Count}) does not match " +
                    $"the ContentLayout dependency count ({resolvedDependencies.Length}); using the external reference table.");
                resolvedDependencies = null;
            }

            int localId = 0;
            m_LocalToDbFileId.Add(localId++, serializedFileId);
            foreach (var extRef in sf.ExternalReferences)
            {
                var name = resolvedDependencies?[localId - 1]
                    ?? extRef.Path.Substring(extRef.Path.LastIndexOf('/') + 1);
                m_LocalToDbFileId.Add(localId++, m_SerializedFileIdProvider.GetId(name));
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

                // Resolve m_GameObject to its analyzer object id for the game_object column, but only
                // when the PPtr is non-null. A null PPtr (m_FileID 0 and m_PathID 0) is expected for
                // any object that is not a component; resolving it would allocate a phantom id for a
                // (file, 0) object that never exists and then surface as a bogus dangling ref.
                object gameObject = "";
                if (randomAccessReader.HasChild("m_GameObject"))
                {
                    var pptr = randomAccessReader["m_GameObject"];
                    var gameObjectFileId = pptr["m_FileID"].GetValue<int>();
                    var gameObjectPathId = pptr["m_PathID"].GetValue<long>();
                    if (gameObjectFileId != 0 || gameObjectPathId != 0)
                    {
                        gameObject = m_ObjectIdProvider.GetId((m_LocalToDbFileId[gameObjectFileId], gameObjectPathId));
                    }
                }
                m_AddObjectCommand.SetValue("game_object", gameObject);

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
            m_WrittenSerializedFileIds.Add(serializedFileId);
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    // Records every referenced object that was assigned an id but never written to the objects
    // table (its serialized file was not part of the analyzed input). Must run after all files are
    // processed, since an id is only known to be dangling once nothing has claimed it as an object.
    // The set of written objects/files is read back from the database so this is robust to every
    // object write site (regular objects, synthetic Scene objects, etc.) without instrumenting them.
    public void FinalizeDatabase()
    {
        // Dangling refs are part of reference tracking, so honor --skip-references (the view that
        // makes them useful joins the refs table, which is empty in that mode anyway).
        if (!m_Initialized || m_SkipReferences)
            return;

        var writtenObjectIds = ReadIntSet("SELECT id FROM objects");
        var writtenFileIds = ReadIntSet("SELECT id FROM serialized_files");

        // Invert the file-name provider so a dangling target's file id maps back to its name.
        var fileIdToName = new Dictionary<int, string>();
        foreach (var entry in m_SerializedFileIdProvider.Entries)
            fileIdToName[entry.Value] = entry.Key;

        using var transaction = m_Database.BeginTransaction();
        try
        {
            foreach (var entry in m_ObjectIdProvider.Entries)
            {
                var objectId = entry.Value;
                if (writtenObjectIds.Contains(objectId))
                    continue;

                var (fileId, pathId) = entry.Key;

                // Ensure the (un-analyzed) target file has a serialized_files row so the dangling
                // ref can name it. We have to put null for archive - even if the target file is inside an AssetBundle
                // or other archive file, because we simply don't have that information.  This is fundamental to
                // how references work in Unity.
                if (writtenFileIds.Add(fileId))
                {
                    m_AddSerializedFileCommand.SetTransaction(transaction);
                    m_AddSerializedFileCommand.SetValue("id", fileId);
                    m_AddSerializedFileCommand.SetValue("archive", null);
                    m_AddSerializedFileCommand.SetValue("name",
                        fileIdToName.TryGetValue(fileId, out var name) ? name : "");
                    m_AddSerializedFileCommand.ExecuteNonQuery();
                }

                m_AddDanglingRefCommand.SetTransaction(transaction);
                m_AddDanglingRefCommand.SetValue("id", objectId);
                m_AddDanglingRefCommand.SetValue("object_id", pathId);
                m_AddDanglingRefCommand.SetValue("serialized_file", fileId);
                m_AddDanglingRefCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private HashSet<int> ReadIntSet(string sql)
    {
        var result = new HashSet<int>();
        using var command = m_Database.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetInt32(0));
        return result;
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
        m_AddArchiveCommand.Dispose();
        m_AddSerializedFileCommand.Dispose();
        m_AddReferenceCommand.Dispose();
        m_AddPropertyNameCommand.Dispose();
        m_AddPropertyTypeCommand.Dispose();
        m_AddObjectCommand.Dispose();
        m_AddTypeCommand.Dispose();
        m_InsertDepCommand.Dispose();
        m_AddDanglingRefCommand.Dispose();

        m_LastId.Dispose();
    }
}
