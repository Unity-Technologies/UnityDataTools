using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite;

public class SQLiteWriter : IWriter
{
    private HashSet<int> m_TypeSet = new ();

    private int m_CurrentAssetBundleId = -1;
    private int m_NextAssetBundleId = 0;

    private string m_DatabaseName;
    private bool m_ExtractReferences;

    private IdProvider<string> m_SerializedFileIdProvider = new ();
    private ObjectIdProvider m_ObjectIdProvider = new ();
    
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
    };

    private SQLiteConnection m_Database;
    private SQLiteCommand m_AddReferenceCommand;
    private SQLiteCommand m_AddAssetBundleCommand;
    private SQLiteCommand m_AddSerializedFileCommand;
    private SQLiteCommand m_AddObjectCommand;
    private SQLiteCommand m_AddTypeCommand;

    public SQLiteWriter(string databaseName, bool extractReferences)
    {
        m_DatabaseName = databaseName;
        m_ExtractReferences = extractReferences;
    }

    public void Begin()
    {
        if (m_Database != null)
        {
            throw new InvalidOperationException("SQLiteWriter.Begin called twice");
        }
        
        m_Database = new SQLiteConnection($"Data Source={m_DatabaseName};Version=3;New=True;Foreign Keys=False;");
        
        SQLiteConnection.CreateFile(m_DatabaseName);
        m_Database.Open();

        using var command = m_Database.CreateCommand();
        command.CommandText = Properties.Resources.Init;
        command.ExecuteNonQuery();

        foreach (var handler in m_Handlers.Values)
        {
            handler.Init(m_Database);
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
        m_AddAssetBundleCommand.Parameters.Add("@id", DbType.Int32);
        m_AddAssetBundleCommand.Parameters.Add("@name", DbType.String);
        m_AddAssetBundleCommand.Parameters.Add("@file_size", DbType.Int64);

        m_AddSerializedFileCommand = m_Database.CreateCommand();
        m_AddSerializedFileCommand.CommandText = "INSERT INTO serialized_files (id, asset_bundle, name) VALUES (@id, @asset_bundle, @name)";
        m_AddSerializedFileCommand.Parameters.Add("@id", DbType.Int32);
        m_AddSerializedFileCommand.Parameters.Add("@asset_bundle", DbType.Int32);
        m_AddSerializedFileCommand.Parameters.Add("@name", DbType.String);

        m_AddReferenceCommand = m_Database.CreateCommand();
        m_AddReferenceCommand.CommandText = "INSERT INTO refs (object, referenced_object, property_path) VALUES (@object, @referenced_object, @property_path)";
        m_AddReferenceCommand.Parameters.Add("@object", DbType.Int64);
        m_AddReferenceCommand.Parameters.Add("@referenced_object", DbType.Int64);
        m_AddReferenceCommand.Parameters.Add("@property_path", DbType.String);
        
        m_AddObjectCommand = m_Database.CreateCommand();
        m_AddObjectCommand.CommandText = "INSERT INTO objects (id, object_id, serialized_file, type, name, game_object, size) VALUES (@id, @object_id, @serialized_file, @type, @name, @game_object, @size)";
        m_AddObjectCommand.Parameters.Add("@id", DbType.Int64);
        m_AddObjectCommand.Parameters.Add("@object_id", DbType.Int64);
        m_AddObjectCommand.Parameters.Add("@serialized_file", DbType.Int32);
        m_AddObjectCommand.Parameters.Add("@type", DbType.Int32);
        m_AddObjectCommand.Parameters.Add("@name", DbType.String);
        m_AddObjectCommand.Parameters.Add("@game_object", DbType.Int64);
        m_AddObjectCommand.Parameters.Add("@size", DbType.Int64);

        m_AddTypeCommand = m_Database.CreateCommand();
        m_AddTypeCommand.CommandText = "INSERT INTO types (id, name) VALUES (@id, @name)";
        m_AddTypeCommand.Parameters.Add("@id", DbType.Int32);
        m_AddTypeCommand.Parameters.Add("@name", DbType.String);
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

    [SuppressMessage("ReSharper.DPA", "DPA0001: Memory allocation issues")]
    public void WriteSerializedFile(string filename, string fullPath)
    {
        using var sf = UnityFileSystem.OpenSerializedFile(fullPath);
        using var reader = new UnityFileReader(fullPath, 64 * 1024 * 1024);
        var pptrReader = new PPtrReader(sf, reader, AddReference);
        
        int serializedFileId = m_SerializedFileIdProvider.GetId(filename.ToLower());
        
        m_LocalToDbFileId.Clear();

        using var transaction = m_Database.BeginTransaction();
        
        try
        {
            m_AddSerializedFileCommand.Parameters["@id"].Value = serializedFileId;
            m_AddSerializedFileCommand.Parameters["@asset_bundle"].Value = m_CurrentAssetBundleId == -1 ? null : m_CurrentAssetBundleId;
            m_AddSerializedFileCommand.Parameters["@name"].Value = filename;
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

                var root = sf.GetTypeTreeRoot(obj.Id);
                var offset = obj.Offset;

                if (!m_TypeSet.Contains(obj.TypeId))
                {
                    m_AddTypeCommand.Parameters["@id"].Value = obj.TypeId;
                    m_AddTypeCommand.Parameters["@name"].Value = root.Type;
                    m_AddTypeCommand.ExecuteNonQuery();

                    m_TypeSet.Add(obj.TypeId);
                }

                var randomAccessReader = new RandomAccessReader(sf, root, reader, offset);

                string name = null;
                long streamDataSize = 0;

                if (m_Handlers.TryGetValue(root.Type, out var handler))
                {
                    handler.Process(m_ObjectIdProvider, currentObjectId, m_LocalToDbFileId, randomAccessReader,
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
                    m_AddObjectCommand.Parameters["@game_object"].Value =
                        m_ObjectIdProvider.GetId((fileId, pptr["m_PathID"].GetValue<long>()));
                }
                else
                {
                    m_AddObjectCommand.Parameters["@game_object"].Value = null;
                }

                m_AddObjectCommand.Parameters["@id"].Value = currentObjectId;
                m_AddObjectCommand.Parameters["@object_id"].Value = obj.Id;
                m_AddObjectCommand.Parameters["@serialized_file"].Value = serializedFileId;
                m_AddObjectCommand.Parameters["@type"].Value = obj.TypeId;
                m_AddObjectCommand.Parameters["@name"].Value = name;
                m_AddObjectCommand.Parameters["@size"].Value = obj.Size + streamDataSize;
                m_AddObjectCommand.ExecuteNonQuery();

                if (m_ExtractReferences)
                {
                    pptrReader.Process(currentObjectId, offset, root);
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
        }
    }

    public void AddReference(long objectId, int fileId, long pathId, string propertyPath)
    {
        var referencedObjectId = m_ObjectIdProvider.GetId((m_LocalToDbFileId[fileId], pathId));
        m_AddReferenceCommand.Parameters["@object"].Value = objectId;
        m_AddReferenceCommand.Parameters["@referenced_object"].Value = referencedObjectId;
        m_AddReferenceCommand.Parameters["@property_path"].Value = propertyPath;
        m_AddReferenceCommand.ExecuteNonQuery();
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

        m_Database.Dispose();
    }
}

public class IdProvider<Key>
{
    private Dictionary<Key, int> m_Ids = new ();

    public int GetId(Key key)
    {
        int id;
        
        if (m_Ids.TryGetValue(key, out id))
        {
            return id;
        }

        id = m_Ids.Count;
        m_Ids.Add(key, id);

        return id;
    }
}

public class ObjectIdProvider : IdProvider<(int, long)> {}