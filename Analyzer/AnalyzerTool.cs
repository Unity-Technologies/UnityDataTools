using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;


namespace UnityDataTools.Analyzer
{
    public class AnalyzerTool
    {
        HashSet<int> m_TypeSet = new HashSet<int>();

        int m_NextAssetBundleId = 0;
        int m_NextSerializedFileId = 0;
        long m_NextObjectId = 0;
        bool m_extractReferences = false;

        Dictionary<string, int> m_SerializedFilenameToId = new Dictionary<string, int>();
        Dictionary<(int, long), long> m_PPtrToId = new Dictionary<(int, long), long>();
        Dictionary<string, Processors.IProcessor> m_Processors = new Dictionary<string, Processors.IProcessor>();

        SQLiteCommand m_AddReferenceCommand;

        public int GetSerializedFileId(string filename)
        {
            if (m_SerializedFilenameToId.TryGetValue(filename, out var id))
            {
                return id;
            }

            m_SerializedFilenameToId.Add(filename, m_NextSerializedFileId);

            return m_NextSerializedFileId++;
        }

        public long GetObjectId(int fileId, long pathId)
        {
            if (m_PPtrToId.TryGetValue((fileId, pathId), out var id))
            {
                return id;
            }

            m_PPtrToId.Add((fileId, pathId), m_NextObjectId);

            return m_NextObjectId++;
        }

        public void AddProcessor(string typeName, Processors.IProcessor processor)
        {
            m_Processors.Add(typeName, processor);
        }

        public int Analyze(string path, string databaseName, string searchPattern, bool extractReferences)
        {
            m_extractReferences = extractReferences;

            using var db = new SQLiteConnection($"Data Source={databaseName};Version=3;New=True;Foreign Keys=False;");
            try
            {
                SQLiteConnection.CreateFile(databaseName);
                db.Open();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error creating database: {e.Message}");
                return 1;
            }

            using var command = db.CreateCommand();
            command.CommandText = Properties.Resources.Init;
            command.ExecuteNonQuery();

            foreach (var processor in m_Processors.Values)
            {
                processor.Init(db);
            }

            using var addAssetBundleCommand = db.CreateCommand();
            addAssetBundleCommand.CommandText = "INSERT INTO asset_bundles (id, name, file_size) VALUES (@id, @name, @file_size)";
            addAssetBundleCommand.Parameters.Add("@id", DbType.Int32);
            addAssetBundleCommand.Parameters.Add("@name", DbType.String);
            addAssetBundleCommand.Parameters.Add("@file_size", DbType.Int64);

            using var addSerializedFileCommand = db.CreateCommand();
            addSerializedFileCommand.CommandText = "INSERT INTO serialized_files (id, asset_bundle, name) VALUES (@id, @asset_bundle, @name)";
            addSerializedFileCommand.Parameters.Add("@id", DbType.Int32);
            addSerializedFileCommand.Parameters.Add("@asset_bundle", DbType.Int32);
            addSerializedFileCommand.Parameters.Add("@name", DbType.String);

            m_AddReferenceCommand = db.CreateCommand();
            m_AddReferenceCommand.CommandText = "INSERT INTO refs (object, referenced_object, property_path) VALUES (@object, @referenced_object, @property_path)";
            m_AddReferenceCommand.Parameters.Add("@object", DbType.Int64);
            m_AddReferenceCommand.Parameters.Add("@referenced_object", DbType.Int64);
            m_AddReferenceCommand.Parameters.Add("@property_path", DbType.String);

            var timer = new Stopwatch();
            timer.Start();

            var files = Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
            int i = 1;
            int lastLength = 0;
            foreach (var file in files)
            {
                try
                {
                    try
                    {
                        using var archive = UnityFileSystem.MountArchive(file, "/");
                        var assetBundleId = m_NextAssetBundleId++;
                        var assetBundleName = Path.GetRelativePath(path, file);

                        var message = $"Processing { i * 100 / files.Length}% ({ i}/{ files.Length}) { assetBundleName}";
                        Console.Write($"\r{message}{new string(' ', Math.Max(0, lastLength - message.Length))}");
                        lastLength = message.Length;

                        addAssetBundleCommand.Parameters["@id"].Value = assetBundleId;
                        addAssetBundleCommand.Parameters["@name"].Value = assetBundleName;
                        addAssetBundleCommand.Parameters["@file_size"].Value = new FileInfo(file).Length;
                        addAssetBundleCommand.ExecuteNonQuery();

                        foreach (var node in archive.Nodes)
                        {
                            if (node.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                            {
                                using var transaction = db.BeginTransaction();

                                try
                                {
                                    int serializedFileId = GetSerializedFileId(node.Path.ToLower());
                                    addSerializedFileCommand.Parameters["@id"].Value = serializedFileId;
                                    addSerializedFileCommand.Parameters["@asset_bundle"].Value = assetBundleId;
                                    addSerializedFileCommand.Parameters["@name"].Value = node.Path;
                                    addSerializedFileCommand.ExecuteNonQuery();

                                    ProcessSerializedFile("/" + node.Path, serializedFileId, db);
                                    transaction.Commit();
                                }
                                catch
                                {
                                    transaction.Rollback();
                                    throw;
                                }
                            }
                        }
                    }
                    catch (NotSupportedException)
                    {
                        using var transaction = db.BeginTransaction();

                        try
                        {
                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write(new string(' ', Console.BufferWidth));
                            Console.Write($"\rProcessing {i * 100 / files.Length}% ({i}/{files.Length}) {file}");

                            int serializedFileId = GetSerializedFileId(file.ToLower());
                            addSerializedFileCommand.Parameters["@id"].Value = serializedFileId;
                            addSerializedFileCommand.Parameters["@asset_bundle"].Value = null;
                            addSerializedFileCommand.Parameters["@name"].Value = file;
                            addSerializedFileCommand.ExecuteNonQuery();

                            ProcessSerializedFile(file, serializedFileId, db);
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                catch (Exception)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"Error processing file {file}.");
                }

                ++i;
            }

            Console.WriteLine();
            Console.WriteLine("Finalizing database...");
            using var finalizeCommand = db.CreateCommand();
            finalizeCommand.CommandText = Properties.Resources.Finalize;
            finalizeCommand.ExecuteNonQuery();

            timer.Stop();
            Console.WriteLine();
            Console.WriteLine($"Total time: {(timer.Elapsed.TotalMilliseconds / 1000.0):F3} s");

            m_AddReferenceCommand.Dispose();

            return 0;
        }

        void ProcessSerializedFile(string path, int serializedFileId, SQLiteConnection db)
        {
            using var reader = new UnityFileReader(path, 64 * 1024 * 1024);
            using var sf = UnityFileSystem.OpenSerializedFile(path);

            // Used to map PPtr fileId to its corresponding serialized file id in the database.
            var localToDbFileId = new Dictionary<int, int>();

            using var addObjectCommand = db.CreateCommand();
            addObjectCommand.CommandText = "INSERT INTO objects (id, object_id, serialized_file, type, name, game_object, size) VALUES (@id, @object_id, @serialized_file, @type, @name, @game_object, @size)";
            addObjectCommand.Parameters.Add("@id", DbType.Int64);
            addObjectCommand.Parameters.Add("@object_id", DbType.Int64);
            addObjectCommand.Parameters.Add("@serialized_file", DbType.Int32);
            addObjectCommand.Parameters.Add("@type", DbType.Int32);
            addObjectCommand.Parameters.Add("@name", DbType.String);
            addObjectCommand.Parameters.Add("@game_object", DbType.Int64);
            addObjectCommand.Parameters.Add("@size", DbType.Int64);

            using var addTypeCommand = db.CreateCommand();
            addTypeCommand.CommandText = "INSERT INTO types (id, name) VALUES (@id, @name)";
            addTypeCommand.Parameters.Add("@id", DbType.Int32);
            addTypeCommand.Parameters.Add("@name", DbType.String);

            int localId = 0;
            localToDbFileId.Add(localId++, serializedFileId);
            foreach (var extRef in sf.ExternalReferences)
            {
                localToDbFileId.Add(localId++, GetSerializedFileId(extRef.Path.Substring(extRef.Path.LastIndexOf('/') + 1).ToLower()));
            }

            foreach (var obj in sf.Objects)
            {
                var currentObjectId = GetObjectId(serializedFileId, obj.Id);

                var root = sf.GetTypeTreeRoot(obj.Id);
                var offset = obj.Offset;

                if (!m_TypeSet.Contains(obj.TypeId))
                {
                    addTypeCommand.Parameters["@id"].Value = obj.TypeId;
                    addTypeCommand.Parameters["@name"].Value = root.Type;
                    addTypeCommand.ExecuteNonQuery();

                    m_TypeSet.Add(obj.TypeId);
                }

                var randomAccessReader = new RandomAccessReader(root, reader, offset);

                string name = null;
                long streamedDataSize = 0;

                if (m_Processors.TryGetValue(root.Type, out var processor))
                {
                    processor.Process(this, currentObjectId, localToDbFileId, randomAccessReader, out name, out streamedDataSize);
                }
                else if (randomAccessReader.HasChild("m_Name"))
                {
                    name = randomAccessReader["m_Name"].GetValue<string>();
                }

                if (randomAccessReader.HasChild("m_GameObject"))
                {
                    var pptr = randomAccessReader["m_GameObject"];
                    var fileId = localToDbFileId[pptr["m_FileID"].GetValue<int>()];
                    addObjectCommand.Parameters["@game_object"].Value = GetObjectId(fileId, pptr["m_PathID"].GetValue<long>());
                }
                else
                {
                    addObjectCommand.Parameters["@game_object"].Value = null;
                }

                addObjectCommand.Parameters["@id"].Value = currentObjectId;
                addObjectCommand.Parameters["@object_id"].Value = obj.Id;
                addObjectCommand.Parameters["@serialized_file"].Value = serializedFileId;
                addObjectCommand.Parameters["@type"].Value = obj.TypeId;
                addObjectCommand.Parameters["@name"].Value = name;
                addObjectCommand.Parameters["@size"].Value = obj.Size + streamedDataSize;
                addObjectCommand.ExecuteNonQuery();

                if (m_extractReferences)
                {
                    var pptrReader = new PPtrReader(root, reader, offset, (fileId, pathId, propertyPath) => AddReference(currentObjectId, GetObjectId(localToDbFileId[fileId], pathId), propertyPath));
                }
            }
        }

        void AddReference(long objectId, long referencedObjectId, string propertyPath)
        {
            m_AddReferenceCommand.Parameters["@object"].Value = objectId;
            m_AddReferenceCommand.Parameters["@referenced_object"].Value = referencedObjectId;
            m_AddReferenceCommand.Parameters["@property_path"].Value = propertyPath;
            m_AddReferenceCommand.ExecuteNonQuery();
        }
    }
}
