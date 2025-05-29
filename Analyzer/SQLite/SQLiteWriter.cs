using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Text.RegularExpressions;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;
using UnityDataTools.Analyzer.Build;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace UnityDataTools.Analyzer.SQLite;

public class SQLiteWriter : IWriter
{
    private HashSet<int> m_TypeSet = new ();

    private int m_CurrentAssetBundleId = -1;
    private int m_NextAssetBundleId = 0;

    private string m_DatabaseName;
    private bool m_SkipReferences;

    private Util.IdProvider<string> m_SerializedFileIdProvider = new ();
    private Util.ObjectIdProvider m_ObjectIdProvider = new ();

    private Regex m_RegexSceneFile = new(@"BuildPlayer-([^\.]+)(?:\.sharedAssets)?");

    // Used to map PPtr fileId to its corresponding serialized file id in the database.
    Dictionary<int, int> m_LocalToDbFileId = new ();

    private Dictionary<string, ISQLiteHandler> m_Handlers = new ()
    {
        { "Mesh", new MeshHandler() },
        { "Texture2D", new Texture2DHandler() },
        { "Shader", new ShaderHandler() },
        { "AudioClip", new AudioClipHandler() },
        { "AnimationClip", new AnimationClipHandler() },
        { "AssetBundle", new AssetBundleHandler() },
        { "PreloadData", new PreloadDataHandler() },
    };

    private SqliteConnection m_Database;
    private SqliteCommand m_AddReferenceCommand = new SqliteCommand();
    private SqliteCommand m_AddAssetBundleCommand = new SqliteCommand();
    private SqliteCommand m_AddSerializedFileCommand = new SqliteCommand();
    private SqliteCommand m_AddObjectCommand = new SqliteCommand();
    private SqliteCommand m_AddTypeCommand = new SqliteCommand();
    private SqliteCommand m_InsertDepCommand = new SqliteCommand();
    private SqliteCommand m_InsertBuild = new SqliteCommand();
    private SqliteCommand m_ExplicitAsset = new SqliteCommand();
    private SqliteCommand m_LastId = new SqliteCommand();
    private SqliteTransaction m_CurrentTransaction = null;
    public SQLiteWriter(string databaseName, bool skipReferences)
    {
        m_DatabaseName = databaseName;
        m_SkipReferences = skipReferences;
    }

    public void Begin()
    {
        if (m_Database != null)
        {
            throw new InvalidOperationException("SQLiteWriter.Begin called twice");
        }
        SqliteConnectionStringBuilder builder = new();
        builder.DataSource = m_DatabaseName;
        builder.Mode = SqliteOpenMode.ReadWriteCreate;
        m_Database = new SqliteConnection(builder.ConnectionString);
        File.WriteAllBytes(m_DatabaseName, Array.Empty<byte>());
        try
        {
            m_Database.Open();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error creating database: {e.Message}");
        }

        using var command = m_Database.CreateCommand();
        command.CommandText = Properties.Resources.Init;
        command.ExecuteNonQuery();

        foreach (var handler in m_Handlers.Values)
        {
            // Console.WriteLine($"Init handler: {handler.GetType().Name}");
            // Console.WriteLine($"Connection state before init: {m_Database.State}");
            handler.Init(m_Database);
            // Console.WriteLine($"Connection state after init: {m_Database.State}");

        }

        CreateSQLiteCommands();
    }

    public void End()
    {
        if (m_Database == null)
        {
            throw new InvalidOperationException("SQLiteWriter.End called before SQLiteWriter.Begin");
        }

        foreach (var handler in m_Handlers.Values)
        {
            handler.Finalize(m_Database);
        }

        using var finalizeCommand = m_Database.CreateCommand();
        finalizeCommand.CommandText = Properties.Resources.Finalize;
        finalizeCommand.ExecuteNonQuery();
    }

    private void CreateSQLiteCommands()
    {
        m_AddAssetBundleCommand = m_Database.CreateCommand();
        m_AddAssetBundleCommand.CommandText = "INSERT INTO asset_bundles (id, name, file_size) VALUES (@id, @name, @file_size)";
        m_AddAssetBundleCommand.Parameters.Add("@id", SqliteType.Integer);
        m_AddAssetBundleCommand.Parameters.Add("@name", SqliteType.Text);
        m_AddAssetBundleCommand.Parameters.Add("@file_size", SqliteType.Integer);

        m_AddSerializedFileCommand = m_Database.CreateCommand();
        m_AddSerializedFileCommand.CommandText = "INSERT INTO serialized_files (id, asset_bundle, name) VALUES (@id, @asset_bundle, @name)";
        m_AddSerializedFileCommand.Parameters.Add("@id", SqliteType.Integer);
        m_AddSerializedFileCommand.Parameters.Add("@asset_bundle", SqliteType.Integer);
        m_AddSerializedFileCommand.Parameters.Add("@name", SqliteType.Text);

        m_AddReferenceCommand = m_Database.CreateCommand();
        m_AddReferenceCommand.CommandText = "INSERT INTO refs (object, referenced_object, property_path, property_type) VALUES (@object, @referenced_object, @property_path, @property_type)";
        m_AddReferenceCommand.Parameters.Add("@object", SqliteType.Integer);
        m_AddReferenceCommand.Parameters.Add("@referenced_object", SqliteType.Integer);
        m_AddReferenceCommand.Parameters.Add("@property_path", SqliteType.Text);
        m_AddReferenceCommand.Parameters.Add("@property_type", SqliteType.Text);

        m_AddObjectCommand = m_Database.CreateCommand();
        m_AddObjectCommand.CommandText = "INSERT INTO objects (id, object_id, serialized_file, type, name, game_object, size, crc32) VALUES (@id, @object_id, @serialized_file, @type, @name, @game_object, @size, @crc32)";
        m_AddObjectCommand.Parameters.Add("@id", SqliteType.Integer);
        m_AddObjectCommand.Parameters.Add("@object_id", SqliteType.Integer);
        m_AddObjectCommand.Parameters.Add("@serialized_file", SqliteType.Integer);
        m_AddObjectCommand.Parameters.Add("@type", SqliteType.Integer);
        m_AddObjectCommand.Parameters.Add("@name", SqliteType.Text);
        m_AddObjectCommand.Parameters.Add("@game_object", SqliteType.Integer);
        m_AddObjectCommand.Parameters.Add("@size", SqliteType.Integer);
        m_AddObjectCommand.Parameters.Add("@crc32", SqliteType.Integer);

        m_AddTypeCommand = m_Database.CreateCommand();
        m_AddTypeCommand.CommandText = "INSERT INTO types (id, name) VALUES (@id, @name)";
        m_AddTypeCommand.Parameters.Add("@id", SqliteType.Integer);
        m_AddTypeCommand.Parameters.Add("@name", SqliteType.Text);

        m_InsertDepCommand = m_Database.CreateCommand();
        m_InsertDepCommand.CommandText = "INSERT INTO asset_dependencies(object, dependency) VALUES(@object, @dependency)";
        m_InsertDepCommand.Parameters.Add("@object", SqliteType.Integer);
        m_InsertDepCommand.Parameters.Add("@dependency", SqliteType.Integer);

        m_InsertBuild = m_Database.CreateCommand();
        m_InsertBuild.CommandText = "INSERT INTO build_layouts (name, build_target, start_time, duration, error, package_version, player_version, build_script, result_hash, type, unity_version) VALUES (@name, @build_target, @start_time, @duration, @error, @package_version, @player_version, @build_script, @result_hash, @type, @unity_version)";
        m_InsertBuild.Parameters.Add("@name", SqliteType.Text);
        m_InsertBuild.Parameters.Add("@build_target", SqliteType.Integer);
        m_InsertBuild.Parameters.Add("@start_time", SqliteType.Integer);
        m_InsertBuild.Parameters.Add("@duration", SqliteType.Real);
        m_InsertBuild.Parameters.Add("@error", SqliteType.Text);
        m_InsertBuild.Parameters.Add("@package_version", SqliteType.Text);
        m_InsertBuild.Parameters.Add("@player_version", SqliteType.Text);
        m_InsertBuild.Parameters.Add("@build_script", SqliteType.Text);
        m_InsertBuild.Parameters.Add("@result_hash", SqliteType.Text);
        m_InsertBuild.Parameters.Add("@type", SqliteType.Integer);
        m_InsertBuild.Parameters.Add("@unity_version", SqliteType.Text);

        m_ExplicitAsset = m_Database.CreateCommand();
        m_ExplicitAsset.CommandText =
        "INSERT INTO build_layout_explicit_assets (id, build_id, bundle, file, asset_hash, asset_path, addressable_name, externally_referenced_assets, group_guid, guid, internal_id, internal_referenced_explicit_assets, internal_referenced_other_assets, labels, streamed_size, serialized_size, main_asset_type) VALUES (@id, @build_id, @bundle, @file, @asset_hash, @asset_path, @addressable_name, @externally_referenced_assets, @group_guid, @guid, @internal_id, @internal_referenced_explicit_assets, @internal_referenced_other_assets, @labels, @streamed_size, @serialized_size, @main_asset_type)";
        m_ExplicitAsset.Parameters.Add("@id", SqliteType.Integer);
        m_ExplicitAsset.Parameters.Add("@build_id", SqliteType.Integer);
        m_ExplicitAsset.Parameters.Add("@bundle", SqliteType.Integer);
        m_ExplicitAsset.Parameters.Add("@file", SqliteType.Integer);
        m_ExplicitAsset.Parameters.Add("@asset_hash", SqliteType.Text);
        m_ExplicitAsset.Parameters.Add("@asset_path", SqliteType.Text);
        m_ExplicitAsset.Parameters.Add("@addressable_name", SqliteType.Text);
        m_ExplicitAsset.Parameters.Add("@externally_referenced_assets", SqliteType.Text); // JSONB type in SQLite uses TEXT
        m_ExplicitAsset.Parameters.Add("@group_guid", SqliteType.Text);
        m_ExplicitAsset.Parameters.Add("@guid", SqliteType.Text);
        m_ExplicitAsset.Parameters.Add("@internal_id", SqliteType.Text);
        m_ExplicitAsset.Parameters.Add("@internal_referenced_explicit_assets", SqliteType.Text); // JSONB type in SQLite uses TEXT
        m_ExplicitAsset.Parameters.Add("@internal_referenced_other_assets", SqliteType.Text); // JSONB type in SQLite uses TEXT
        m_ExplicitAsset.Parameters.Add("@labels", SqliteType.Text); // JSONB type in SQLite uses TEXT
        m_ExplicitAsset.Parameters.Add("@streamed_size", SqliteType.Integer);
        m_ExplicitAsset.Parameters.Add("@serialized_size", SqliteType.Integer);
        m_ExplicitAsset.Parameters.Add("@main_asset_type", SqliteType.Integer);

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
        m_AddAssetBundleCommand.Parameters["@id"].Value = m_CurrentAssetBundleId;
        m_AddAssetBundleCommand.Parameters["@name"].Value = name;
        m_AddAssetBundleCommand.Parameters["@file_size"].Value = size;
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

    public void WriteBuildLayout(string filename, BuildLayout buildLayout)
    {
        using var transaction = m_Database.BeginTransaction();
        m_CurrentTransaction = transaction;

        try
        {
            m_InsertBuild.Transaction = transaction;
            m_InsertBuild.Parameters["@name"].Value = Path.GetFileName(filename);
            m_InsertBuild.Parameters["@build_target"].Value = buildLayout.BuildTarget;
            m_InsertBuild.Parameters["@start_time"].Value = buildLayout.BuildStartTime;
            m_InsertBuild.Parameters["@duration"].Value = buildLayout.Duration;
            m_InsertBuild.Parameters["@error"].Value = buildLayout.BuildError;
            m_InsertBuild.Parameters["@package_version"].Value = buildLayout.PackageVersion;
            m_InsertBuild.Parameters["@player_version"].Value = buildLayout.PlayerBuildVersion;
            m_InsertBuild.Parameters["@build_script"].Value = buildLayout.BuildScript;
            m_InsertBuild.Parameters["@result_hash"].Value = buildLayout.BuildResultHash;
            m_InsertBuild.Parameters["@type"].Value = buildLayout.BuildType;
            m_InsertBuild.Parameters["@unity_version"].Value = buildLayout.UnityVersion;
            m_InsertBuild.ExecuteNonQuery();

            m_LastId.Transaction = transaction;
            long buildId = (long) m_LastId.ExecuteScalar();
            Console.WriteLine($"Build ID: {buildId}");

            foreach (var reference in buildLayout.references.RefIds)
            {
                switch(reference.type.Class)
                {
                    case "BuildLayout/ExplicitAsset":
                        m_ExplicitAsset.Transaction = transaction;
                        m_ExplicitAsset.Parameters["@id"].Value = reference.rid;
                        m_ExplicitAsset.Parameters["@build_id"].Value = buildId;
                        m_ExplicitAsset.Parameters["@bundle"].Value = reference.data.Bundle.rid;
                        m_ExplicitAsset.Parameters["@file"].Value = reference.data.File.rid;
                        m_ExplicitAsset.Parameters["@asset_hash"].Value = reference.data.AssetHash.Hash;
                        m_ExplicitAsset.Parameters["@asset_path"].Value = reference.data.AssetPath;
                        m_ExplicitAsset.Parameters["@addressable_name"].Value = reference.data.AddressableName;
                        m_ExplicitAsset.Parameters["@externally_referenced_assets"].Value = JsonConvert.SerializeObject(reference.data.ExternallyReferencedAssets) ?? "[]";
                        m_ExplicitAsset.Parameters["@group_guid"].Value = reference.data.GroupGuid;
                        m_ExplicitAsset.Parameters["@guid"].Value = reference.data.Guid;
                        m_ExplicitAsset.Parameters["@internal_id"].Value = reference.data.InternalId;
                        m_ExplicitAsset.Parameters["@internal_referenced_explicit_assets"].Value = JsonConvert.SerializeObject(reference.data.InternalReferencedExplicitAssets) ?? "[]";
                        m_ExplicitAsset.Parameters["@internal_referenced_other_assets"].Value = JsonConvert.SerializeObject(reference.data.InternalReferencedOtherAssets) ?? "[]";
                        m_ExplicitAsset.Parameters["@labels"].Value = JsonConvert.SerializeObject(reference.data.Labels) ?? "[]";
                        m_ExplicitAsset.Parameters["@main_asset_type"].Value = reference.data.MainAssetType;
                        m_ExplicitAsset.Parameters["@serialized_size"].Value = reference.data.SerializedSize;
                        m_ExplicitAsset.Parameters["@streamed_size"].Value = reference.data.StreamedSize;
                        m_ExplicitAsset.ExecuteNonQuery();
                        break;
                }
            }

            // do the stuff
            transaction.Commit();
        }
        catch (Exception e)
        {
            transaction.Rollback();
            throw;
        }
    }


    public void WriteSerializedFile(string relativePath, string fullPath, string containingFolder)
    {
        using var sf = UnityFileSystem.OpenSerializedFile(fullPath);
        using var reader = new UnityFileReader(fullPath, 64 * 1024 * 1024);
        using var pptrReader = new PPtrAndCrcProcessor(sf, reader, containingFolder, AddReference);
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
                m_AddObjectCommand.Transaction = transaction;
                m_AddObjectCommand.Parameters["@game_object"].Value = ""; // There is no asscociated GameObject
                m_AddObjectCommand.Parameters["@id"].Value = sceneId;
                m_AddObjectCommand.Parameters["@object_id"].Value = 0;
                m_AddObjectCommand.Parameters["@serialized_file"].Value = serializedFileId;
                // The type is set to -1 which doesn't exist in Unity, but is associated to
                // "Scene" in the database.
                m_AddObjectCommand.Parameters["@type"].Value = -1;
                m_AddObjectCommand.Parameters["@name"].Value = sceneName;
                m_AddObjectCommand.Parameters["@size"].Value = 0;
                m_AddObjectCommand.Parameters["@crc32"].Value = 0;
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
            m_AddSerializedFileCommand.Transaction = transaction;
            m_AddSerializedFileCommand.Parameters["@id"].Value = serializedFileId;
            m_AddSerializedFileCommand.Parameters["@asset_bundle"].Value = m_CurrentAssetBundleId == -1 ? "" : m_CurrentAssetBundleId;
            m_AddSerializedFileCommand.Parameters["@name"].Value = relativePath;
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
                    m_AddTypeCommand.Transaction = transaction;
                    m_AddTypeCommand.Parameters["@id"].Value = obj.TypeId;
                    m_AddTypeCommand.Parameters["@name"].Value = root.Type;
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
                    m_AddObjectCommand.Parameters["@game_object"].Value = gameObjectID;
                }
                else
                {
                    m_AddObjectCommand.Parameters["@game_object"].Value = "";
                }

                if (!m_SkipReferences)
                {
                    crc32 = pptrReader.Process(currentObjectId, offset, root);
                }

                m_AddObjectCommand.Parameters["@id"].Value = currentObjectId;
                m_AddObjectCommand.Parameters["@object_id"].Value = obj.Id;
                m_AddObjectCommand.Parameters["@serialized_file"].Value = serializedFileId;
                m_AddObjectCommand.Parameters["@type"].Value = obj.TypeId;
                m_AddObjectCommand.Parameters["@name"].Value = name;
                m_AddObjectCommand.Parameters["@size"].Value = obj.Size + streamDataSize;
                m_AddObjectCommand.Parameters["@crc32"].Value = crc32;
                m_AddObjectCommand.Transaction = transaction;
                m_AddObjectCommand.ExecuteNonQuery();

                // If this is a Scene AssetBundle, add the object as a depencency of the
                // current scene.
                if (ctx.SceneId != -1)
                {
                    m_InsertDepCommand.Parameters["@object"].Value = ctx.SceneId;
                    m_InsertDepCommand.Parameters["@dependency"].Value = currentObjectId;
                    m_InsertDepCommand.Transaction = transaction;
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

    private int AddReference(long objectId, int fileId, long pathId, string propertyPath, string propertyType)
    {
        var referencedObjectId = m_ObjectIdProvider.GetId((m_LocalToDbFileId[fileId], pathId));
        m_AddReferenceCommand.Transaction = m_CurrentTransaction;
        m_AddReferenceCommand.Parameters["@object"].Value = objectId;
        m_AddReferenceCommand.Parameters["@referenced_object"].Value = referencedObjectId;
        m_AddReferenceCommand.Parameters["@property_path"].Value = propertyPath;
        m_AddReferenceCommand.Parameters["@property_type"].Value = propertyType;
        m_AddReferenceCommand.ExecuteNonQuery();

        return referencedObjectId;
    }

    public void Dispose()
    {
        foreach (var handler in m_Handlers.Values)
        {
            handler.Dispose();
        }

        m_AddAssetBundleCommand.Dispose();
        m_AddSerializedFileCommand.Dispose();
        m_AddReferenceCommand.Dispose();
        m_AddObjectCommand.Dispose();
        m_AddTypeCommand.Dispose();
        m_InsertDepCommand.Dispose();

        m_Database.Dispose();
    }
}
